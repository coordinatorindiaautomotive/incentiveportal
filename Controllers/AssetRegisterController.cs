using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class AssetRegisterController(
    IncentiveDbContext db,
    ICurrentUser currentUser
) : Controller
{
    private bool IsHO() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance) ||
        currentUser.IsInRole(AppRoles.Auditor);

    private bool CanEdit() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance) ||
        currentUser.IsInRole(AppRoles.BranchManager);

    private IQueryable<ItAsset> ScopedAssets() =>
        IsHO()
            ? db.ItAssets.Include(x => x.Branch)
            : db.ItAssets.Include(x => x.Branch)
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
    public async Task<IActionResult> GetMasterOptions(CancellationToken ct)
    {
        // Returns lookup lists for the client UI select components
        var allMasters = await db.ItMasterDatas
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Type, x.Name, x.Code })
            .ToListAsync(ct);

        return Json(new {
            categories = allMasters.Where(x => x.Type == "AssetCategory").ToList(),
            types = allMasters.Where(x => x.Type == "AssetType").ToList(),
            brands = allMasters.Where(x => x.Type == "AssetBrand").ToList(),
            models = allMasters.Where(x => x.Type == "AssetModel").ToList(),
            vendors = allMasters.Where(x => x.Type == "Vendor").ToList(),
            locations = allMasters.Where(x => x.Type == "Location").ToList(),
            departments = allMasters.Where(x => x.Type == "Department").ToList(),
            employees = allMasters.Where(x => x.Type == "Employee").ToList(),
            statuses = allMasters.Where(x => x.Type == "Status").ToList(),
            disposalReasons = allMasters.Where(x => x.Type == "DisposalReason").ToList(),
            costCenters = allMasters.Where(x => x.Type == "CostCenter").ToList(),
            amcProviders = allMasters.Where(x => x.Type == "AMCProvider").ToList(),
            warrantyProviders = allMasters.Where(x => x.Type == "WarrantyProvider").ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssets(
        int? branchId, int? categoryId, int? statusId, string? condition,
        string? search, int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        var q = ScopedAssets();
        if (branchId.HasValue && branchId > 0) q = q.Where(x => x.BranchId == branchId);
        if (categoryId.HasValue && categoryId > 0) q = q.Where(x => x.CategoryId == categoryId);
        if (statusId.HasValue && statusId > 0) q = q.Where(x => x.AssetStatusId == statusId);
        if (!string.IsNullOrWhiteSpace(condition)) q = q.Where(x => x.Condition == condition);
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(x => x.Name.Contains(search) || x.AssetCode.Contains(search) ||
                              x.SerialNumber.Contains(search) || x.AssetTag.Contains(search));
        }

        var total = await q.CountAsync(ct);
        var rawItems = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // Fetch lookup names in bulk
        var masterIds = rawItems.SelectMany(x => new[] {
            x.CategoryId, x.TypeId, x.BrandId, x.ModelId, x.VendorId,
            x.LocationId, x.DepartmentId, x.AssignedEmployeeId ?? 0, x.AssetStatusId
        }).Distinct().ToList();

        var masterMap = await db.ItMasterDatas
            .Where(x => masterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rawItems.Select(x => new {
            x.Id, x.AssetCode, x.Name, x.BranchId,
            category = masterMap.TryGetValue(x.CategoryId, out var c) ? c : "Other",
            type = masterMap.TryGetValue(x.TypeId, out var t) ? t : "Other",
            brand = masterMap.TryGetValue(x.BrandId, out var b) ? b : "Other",
            model = masterMap.TryGetValue(x.ModelId, out var m) ? m : "Other",
            vendor = masterMap.TryGetValue(x.VendorId, out var v) ? v : "Other",
            location = masterMap.TryGetValue(x.LocationId, out var loc) ? loc : "Other",
            department = masterMap.TryGetValue(x.DepartmentId, out var d) ? d : "Other",
            employee = x.AssignedEmployeeId.HasValue && masterMap.TryGetValue(x.AssignedEmployeeId.Value, out var emp) ? emp : "Unassigned",
            status = masterMap.TryGetValue(x.AssetStatusId, out var s) ? s : "Active",
            x.CategoryId, x.TypeId, x.BrandId, x.ModelId, x.VendorId, x.LocationId, x.DepartmentId, x.AssignedEmployeeId, x.AssetStatusId,
            x.SerialNumber, x.AssetTag, x.PurchaseCost, x.DepreciationRatePercent, x.PurchaseOrder, x.Condition, x.Remarks,
            purchaseDate = x.PurchaseDate.ToString("yyyy-MM-dd"),
            warrantyEnd = x.WarrantyEnd.HasValue ? x.WarrantyEnd.Value.ToString("yyyy-MM-dd") : string.Empty,
            amcEnd = x.AmcEnd.HasValue ? x.AmcEnd.Value.ToString("yyyy-MM-dd") : string.Empty,
            branchCode = x.Branch.Code, branchName = x.Branch.Name,
            createdAt = x.CreatedAt.ToString("dd MMM yyyy")
        }).ToList();

        return Json(new { total, page, pageSize, items });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ItAsset model, CancellationToken ct)
    {
        if (!CanEdit()) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Name)) return BadRequest(new { message = "Asset name is required." });
        if (model.BranchId <= 0) return BadRequest(new { message = "Branch is required." });
        if (!IsHO() && currentUser.BranchId.HasValue && model.BranchId != currentUser.BranchId) return Forbid();

        if (model.Id == 0)
        {
            var branch = await db.Branches.FindAsync(new object[] { model.BranchId }, ct);
            var prefix = (branch?.Code ?? "XX").ToUpperInvariant();
            var lastSeq = await db.ItAssets.CountAsync(x => x.BranchId == model.BranchId, ct);
            model.AssetCode = $"{prefix}-{(lastSeq + 1):D4}";
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            
            db.ItAssets.Add(model);

            // Log Lifecycle creation
            var log = new ItAssetHistory {
                Asset = model,
                ActionType = "Purchase",
                ToBranchId = model.BranchId,
                ToUserId = model.AssignedEmployeeId,
                Reason = "Asset introduced into inventory registry.",
                ApprovedBy = currentUser.UserName,
                Details = $"Initialized asset {model.Name} with status: Active."
            };
            db.ItAssetHistories.Add(log);
        }
        else
        {
            var existing = await db.ItAssets.FindAsync(new object[] { model.Id }, ct);
            if (existing == null) return NotFound(new { message = "Asset not found." });
            if (!IsHO() && existing.BranchId != currentUser.BranchId) return Forbid();

            // Track changes for lifecycle audit logs
            var sb = new StringBuilder();
            if (existing.AssetStatusId != model.AssetStatusId) sb.AppendLine("Status updated.");
            if (existing.BranchId != model.BranchId) sb.AppendLine($"Transferred from Branch ID {existing.BranchId} to {model.BranchId}.");
            if (existing.AssignedEmployeeId != model.AssignedEmployeeId) sb.AppendLine($"Reallocated from Employee ID {existing.AssignedEmployeeId} to {model.AssignedEmployeeId}.");

            existing.Name = model.Name;
            existing.CategoryId = model.CategoryId;
            existing.TypeId = model.TypeId;
            existing.BrandId = model.BrandId;
            existing.ModelId = model.ModelId;
            existing.SerialNumber = model.SerialNumber;
            existing.AssetTag = model.AssetTag;
            existing.PurchaseDate = model.PurchaseDate;
            existing.PurchaseCost = model.PurchaseCost;
            existing.VendorId = model.VendorId;
            existing.InvoiceNumber = model.InvoiceNumber;
            existing.WarrantyStart = model.WarrantyStart;
            existing.WarrantyEnd = model.WarrantyEnd;
            existing.AmcStart = model.AmcStart;
            existing.AmcEnd = model.AmcEnd;
            existing.AmcProviderId = model.AmcProviderId;
            existing.WarrantyProviderId = model.WarrantyProviderId;
            
            var oldBranch = existing.BranchId;
            var oldUser = existing.AssignedEmployeeId;

            existing.BranchId = model.BranchId;
            existing.LocationId = model.LocationId;
            existing.DepartmentId = model.DepartmentId;
            existing.AssignedEmployeeId = model.AssignedEmployeeId;
            existing.AssetStatusId = model.AssetStatusId;
            existing.Condition = model.Condition;
            existing.CurrentUserId = model.CurrentUserId;
            existing.PurchaseOrder = model.PurchaseOrder;
            existing.DepreciationRatePercent = model.DepreciationRatePercent;
            existing.InsuranceDetails = model.InsuranceDetails;
            existing.Remarks = model.Remarks;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;

            if (sb.Length > 0)
            {
                var action = "Update";
                if (oldBranch != model.BranchId) action = "Transfer";
                else if (oldUser != model.AssignedEmployeeId) action = "Allocate";

                var log = new ItAssetHistory {
                    AssetId = model.Id,
                    ActionType = action,
                    FromBranchId = oldBranch,
                    ToBranchId = model.BranchId,
                    FromUserId = oldUser,
                    ToUserId = model.AssignedEmployeeId,
                    Reason = "General update or movement trigger.",
                    ApprovedBy = currentUser.UserName,
                    Details = sb.ToString()
                };
                db.ItAssetHistories.Add(log);
            }
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Asset saved successfully.", assetCode = model.AssetCode });
    }

    [HttpPost]
    public async Task<IActionResult> AllocateOrTransfer([FromBody] ItAssetHistory movement, CancellationToken ct)
    {
        if (!CanEdit()) return Forbid();
        var asset = await db.ItAssets.FindAsync(new object[] { movement.AssetId }, ct);
        if (asset == null || asset.IsDeleted) return NotFound(new { message = "Asset not found." });

        movement.FromBranchId = asset.BranchId;
        movement.FromUserId = asset.AssignedEmployeeId;
        movement.TransactionDate = DateTime.UtcNow;
        movement.ApprovedBy = currentUser.UserName;
        movement.ApprovalStatus = "Approved";

        // Apply changes directly to asset
        if (movement.ToBranchId.HasValue && movement.ToBranchId.Value > 0)
        {
            asset.BranchId = movement.ToBranchId.Value;
        }
        if (movement.ToUserId.HasValue)
        {
            asset.AssignedEmployeeId = movement.ToUserId.Value;
            asset.CurrentUserId = movement.ToUserId.Value;
        }

        movement.Details = $"Action '{movement.ActionType}' executed. Location/User mapping updated.";
        db.ItAssetHistories.Add(movement);

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Asset reallocation logged successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> GetAssetHistory(int id, CancellationToken ct)
    {
        var logs = await db.ItAssetHistories
            .Where(x => x.AssetId == id && !x.IsDeleted)
            .OrderByDescending(x => x.TransactionDate)
            .Select(x => new {
                x.ActionType,
                x.Reason,
                x.ApprovedBy,
                x.Details,
                transactionDate = x.TransactionDate.ToString("dd MMM yyyy HH:mm")
            }).ToListAsync(ct);
        return Json(logs);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!CanEdit()) return Forbid();
        var existing = await db.ItAssets.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "Asset not found." });
        if (!IsHO() && existing.BranchId != currentUser.BranchId) return Forbid();

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = currentUser.UserName;

        // Log disposal
        var log = new ItAssetHistory {
            AssetId = id,
            ActionType = "Disposal",
            Reason = "User requested asset removal.",
            ApprovedBy = currentUser.UserName,
            Details = "Flagged asset as deleted."
        };
        db.ItAssetHistories.Add(log);

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Asset deleted." });
    }

    [HttpGet]
    public IActionResult GetQrCode(int id)
    {
        var asset = db.ItAssets.FirstOrDefault(x => x.Id == id && !x.IsDeleted);
        if (asset == null) return NotFound();

        var code = asset.AssetCode;

        var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100"" width=""120"" height=""120"">
            <rect width=""100"" height=""100"" fill=""#ffffff""/>
            <rect x=""10"" y=""10"" width=""20"" height=""20"" fill=""none"" stroke=""#09192c"" stroke-width=""4""/>
            <rect x=""14"" y=""14"" width=""12"" height=""12"" fill=""#09192c""/>
            <rect x=""70"" y=""10"" width=""20"" height=""20"" fill=""none"" stroke=""#09192c"" stroke-width=""4""/>
            <rect x=""74"" y=""14"" width=""12"" height=""12"" fill=""#09192c""/>
            <rect x=""10"" y=""70"" width=""20"" height=""20"" fill=""none"" stroke=""#09192c"" stroke-width=""4""/>
            <rect x=""14"" y=""74"" width=""12"" height=""12"" fill=""#09192c""/>
            <rect x=""40"" y=""15"" width=""6"" height=""6"" fill=""#0062ff""/>
            <rect x=""50"" y=""20"" width=""8"" height=""4"" fill=""#09192c""/>
            <rect x=""42"" y=""35"" width=""12"" height=""6"" fill=""#09192c""/>
            <rect x=""15"" y=""45"" width=""10"" height=""10"" fill=""#0062ff""/>
            <rect x=""75"" y=""45"" width=""10"" height=""10"" fill=""#09192c""/>
            <rect x=""45"" y=""55"" width=""14"" height=""8"" fill=""#09192c""/>
            <rect x=""40"" y=""75"" width=""18"" height=""6"" fill=""#09192c""/>
            <rect x=""70"" y=""75"" width=""8"" height=""8"" fill=""#0062ff""/>
            <text x=""50"" y=""96"" font-family=""monospace"" font-size=""6"" text-anchor=""middle"" fill=""#64748b"">{code}</text>
        </svg>";

        return Content(svg, "image/svg+xml");
    }

    [HttpGet]
    public async Task<IActionResult> Export(int? branchId, int? categoryId, int? statusId, CancellationToken ct)
    {
        var q = ScopedAssets();
        if (branchId.HasValue && branchId > 0) q = q.Where(x => x.BranchId == branchId);
        if (categoryId.HasValue && categoryId > 0) q = q.Where(x => x.CategoryId == categoryId);
        if (statusId.HasValue && statusId > 0) q = q.Where(x => x.AssetStatusId == statusId);

        var items = await q.OrderBy(x => x.Branch.Code).ThenBy(x => x.AssetCode).ToListAsync(ct);

        var masterIds = items.SelectMany(x => new[] {
            x.CategoryId, x.TypeId, x.BrandId, x.ModelId, x.VendorId,
            x.LocationId, x.DepartmentId, x.AssignedEmployeeId ?? 0, x.AssetStatusId
        }).Distinct().ToList();

        var masterMap = await db.ItMasterDatas
            .Where(x => masterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Asset Code,Branch,Category,Type,Brand,Model,Name,Serial No.,Asset Tag,Purchase Date,Purchase Cost,Vendor,Invoice No.,Warranty Expiry,Status,Location,Department,Assigned To,PO,Depreciation %,Insurance,Remarks");

        foreach (var a in items)
        {
            static string E(string? s) => $"\"{(s ?? "").Replace("\"", "\"\"")}\"";
            
            var cName = masterMap.TryGetValue(a.CategoryId, out var c) ? c : "Other";
            var tName = masterMap.TryGetValue(a.TypeId, out var t) ? t : "Other";
            var bName = masterMap.TryGetValue(a.BrandId, out var b) ? b : "Other";
            var mName = masterMap.TryGetValue(a.ModelId, out var m) ? m : "Other";
            var vName = masterMap.TryGetValue(a.VendorId, out var v) ? v : "Other";
            var locName = masterMap.TryGetValue(a.LocationId, out var loc) ? loc : "Other";
            var dName = masterMap.TryGetValue(a.DepartmentId, out var d) ? d : "Other";
            var empName = a.AssignedEmployeeId.HasValue && masterMap.TryGetValue(a.AssignedEmployeeId.Value, out var emp) ? emp : "Unassigned";
            var statusName = masterMap.TryGetValue(a.AssetStatusId, out var s) ? s : "Active";

            sb.AppendLine(string.Join(",",
                E(a.AssetCode), E(a.Branch?.Code), E(cName), E(tName), E(bName), E(mName), E(a.Name),
                E(a.SerialNumber), E(a.AssetTag), a.PurchaseDate.ToString("yyyy-MM-dd"),
                a.PurchaseCost, E(vName), E(a.InvoiceNumber), 
                a.WarrantyEnd?.ToString("yyyy-MM-dd") ?? "",
                E(statusName), E(locName), E(dName), E(empName),
                E(a.PurchaseOrder), a.DepreciationRatePercent, E(a.InsuranceDetails), E(a.Remarks)));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"Asset_Register_{DateTime.Today:yyyyMMdd}.csv");
    }
}
