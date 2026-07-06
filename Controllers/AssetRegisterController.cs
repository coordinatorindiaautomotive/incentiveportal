using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class AssetRegisterController(
    IncentiveDbContext db,
    ICurrentUser currentUser
) : Controller
{
    private bool IsHO() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance)  ||
        currentUser.IsInRole(AppRoles.Auditor);

    private bool CanEdit() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance)  ||
        currentUser.IsInRole(AppRoles.BranchManager) ||
        currentUser.IsInRole(AppRoles.Associate);

    private bool CanDelete() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance)  ||
        currentUser.IsInRole(AppRoles.BranchManager);

    private IQueryable<AssetItem> ScopedAssets() =>
        IsHO()
            ? db.AssetItems.Include(x => x.Branch)
            : db.AssetItems.Include(x => x.Branch)
                           .Where(x => x.BranchId == currentUser.BranchId);

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetBranches(CancellationToken ct)
    {
        IQueryable<Branch> q = db.Branches.OrderBy(x => x.Name);
        if (!IsHO() && currentUser.BranchId.HasValue)
            q = q.Where(x => x.Id == currentUser.BranchId);
        var list = await q.Select(x => new { x.Id, x.Code, x.Name }).ToListAsync(ct);
        return Json(list);
    }

    [HttpGet]
    public async Task<IActionResult> GetSummary(int? branchId, CancellationToken ct)
    {
        var q = ScopedAssets();
        if (branchId.HasValue && branchId > 0) q = q.Where(x => x.BranchId == branchId);

        var items = await q.Select(x => new { x.Category, x.Status, x.PurchaseCost, x.CurrentValue }).ToListAsync(ct);

        var byCategory = items
            .GroupBy(x => string.IsNullOrEmpty(x.Category) ? "Other" : x.Category)
            .Select(g => new { category = g.Key, count = g.Count(), cost = g.Sum(r => r.PurchaseCost) })
            .OrderByDescending(x => x.count).ToList();

        var byStatus = items.GroupBy(x => x.Status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToList();

        return Json(new {
            totalAssets  = items.Count,
            totalCost    = items.Sum(x => x.PurchaseCost),
            currentValue = items.Sum(x => x.CurrentValue),
            byCategory, byStatus
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets(
        int? branchId, string? category, string? status, string? condition,
        string? search, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var q = ScopedAssets();
        if (branchId.HasValue && branchId > 0) q = q.Where(x => x.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(category))  q = q.Where(x => x.Category  == category);
        if (!string.IsNullOrWhiteSpace(status))    q = q.Where(x => x.Status    == status);
        if (!string.IsNullOrWhiteSpace(condition)) q = q.Where(x => x.Condition == condition);
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(x => x.Name.Contains(search) || x.AssetCode.Contains(search) ||
                              x.SerialNumber.Contains(search) || x.AssignedTo.Contains(search));

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new {
                x.Id, x.AssetCode, x.Category, x.Name, x.Status, x.Condition,
                x.PurchaseCost, x.CurrentValue, x.DepreciationRatePercent,
                x.Manufacturer, x.ModelNumber, x.SerialNumber,
                x.Vendor, x.InvoiceNumber, x.AssetLocation, x.AssignedTo,
                x.Description, x.Remarks, x.BranchId,
                purchaseDate       = x.PurchaseDate.HasValue       ? x.PurchaseDate.Value.ToString("yyyy-MM-dd")       : (string?)null,
                warrantyExpiryDate = x.WarrantyExpiryDate.HasValue ? x.WarrantyExpiryDate.Value.ToString("yyyy-MM-dd") : (string?)null,
                branchCode = x.Branch.Code, branchName = x.Branch.Name,
                createdAt  = x.CreatedAt.ToString("dd MMM yyyy"), x.CreatedBy
            }).ToListAsync(ct);

        return Json(new { total, page, pageSize, items });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] AssetItem model, CancellationToken ct)
    {
        if (!CanEdit()) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Name))     return BadRequest(new { message = "Asset name is required." });
        if (string.IsNullOrWhiteSpace(model.Category)) return BadRequest(new { message = "Category is required." });
        if (model.BranchId <= 0)                       return BadRequest(new { message = "Branch is required." });
        if (!IsHO() && currentUser.BranchId.HasValue && model.BranchId != currentUser.BranchId) return Forbid();

        if (model.Id == 0)
        {
            var branch  = await db.Branches.FindAsync(new object[] { model.BranchId }, ct);
            var prefix  = (branch?.Code ?? "XX").ToUpperInvariant();
            var lastSeq = await db.AssetItems.CountAsync(x => x.BranchId == model.BranchId, ct);
            model.AssetCode = $"{prefix}-{(lastSeq + 1):D3}";
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.AssetItems.Add(model);
        }
        else
        {
            var existing = await db.AssetItems.FindAsync(new object[] { model.Id }, ct);
            if (existing == null) return NotFound(new { message = "Asset not found." });
            if (!IsHO() && existing.BranchId != currentUser.BranchId) return Forbid();

            existing.Category = model.Category; existing.Name = model.Name;
            existing.Description = model.Description; existing.Manufacturer = model.Manufacturer;
            existing.ModelNumber = model.ModelNumber; existing.SerialNumber = model.SerialNumber;
            existing.PurchaseDate = model.PurchaseDate; existing.PurchaseCost = model.PurchaseCost;
            existing.Vendor = model.Vendor; existing.InvoiceNumber = model.InvoiceNumber;
            existing.DepreciationRatePercent = model.DepreciationRatePercent;
            existing.CurrentValue = model.CurrentValue; existing.Condition = model.Condition;
            existing.Status = model.Status; existing.AssetLocation = model.AssetLocation;
            existing.AssignedTo = model.AssignedTo; existing.WarrantyExpiryDate = model.WarrantyExpiryDate;
            existing.Remarks = model.Remarks; existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Asset saved successfully.", assetCode = model.AssetCode });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanDelete()) return Forbid();
        var existing = await db.AssetItems.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "Asset not found." });
        if (!IsHO() && existing.BranchId != currentUser.BranchId) return Forbid();
        existing.IsDeleted = true; existing.UpdatedAt = DateTime.UtcNow; existing.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Asset deleted." });
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? branchId, string? category, string? status, CancellationToken ct)
    {
        var q = ScopedAssets();
        if (branchId.HasValue && branchId > 0)        q = q.Where(x => x.BranchId == branchId);
        if (!string.IsNullOrWhiteSpace(category))     q = q.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(status))       q = q.Where(x => x.Status   == status);

        var items = await q.OrderBy(x => x.Branch.Code).ThenBy(x => x.AssetCode).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Asset Code,Branch,Category,Name,Serial No.,Manufacturer,Model,Purchase Date,Purchase Cost,Current Value,Depreciation %,Condition,Status,Location,Assigned To,Vendor,Invoice No.,Warranty Expiry,Remarks,Created By,Created At");

        foreach (var a in items)
        {
            static string E(string? s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
            sb.AppendLine(string.Join(",",
                E(a.AssetCode), E(a.Branch?.Code), E(a.Category), E(a.Name),
                E(a.SerialNumber), E(a.Manufacturer), E(a.ModelNumber),
                a.PurchaseDate?.ToString("yyyy-MM-dd") ?? "",
                a.PurchaseCost, a.CurrentValue, a.DepreciationRatePercent,
                E(a.Condition), E(a.Status), E(a.AssetLocation), E(a.AssignedTo),
                E(a.Vendor), E(a.InvoiceNumber), a.WarrantyExpiryDate?.ToString("yyyy-MM-dd") ?? "",
                E(a.Remarks), E(a.CreatedBy), a.CreatedAt.ToString("yyyy-MM-dd")));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"Asset_Register_{DateTime.Today:yyyyMMdd}.csv");
    }
}
