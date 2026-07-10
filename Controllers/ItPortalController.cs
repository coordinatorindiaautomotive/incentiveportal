using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class ItPortalController(
    IncentiveDbContext db,
    ICurrentUser currentUser
) : Controller
{
    private bool IsAdmin() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance);

    private bool IsITUser() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance) ||
        currentUser.IsInRole(AppRoles.BranchManager) ||
        currentUser.IsInRole(AppRoles.Auditor);

    // ── PAGE VIEWS ───────────────────────────────────────────────────────────
    public IActionResult Dashboard() => View();
    
    public IActionResult Masters()
    {
        if (!IsAdmin()) return Forbid();
        return View();
    }

    public IActionResult SoftwareLicenses() => View();
    public IActionResult Kb() => View();
    public IActionResult Maintenance() => View();

    // ── DASHBOARD STATS API ──────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var sixtyDaysOut = now.AddDays(60);

        // Asset Stats
        var assetsQuery = db.ItAssets.Where(x => !x.IsDeleted);
        if (!IsITUser() && currentUser.BranchId.HasValue)
            assetsQuery = assetsQuery.Where(x => x.BranchId == currentUser.BranchId);

        var assets = await assetsQuery
            .Select(x => new { x.AssetStatusId, x.CategoryId, x.PurchaseCost, x.WarrantyEnd, x.AmcEnd })
            .ToListAsync(ct);

        // Resolve Status Names
        var statuses = await db.ItMasterDatas
            .Where(x => x.Type == "Status" && !x.IsDeleted && x.IsActive)
            .ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var categories = await db.ItMasterDatas
            .Where(x => x.Type == "AssetCategory" && !x.IsDeleted && x.IsActive)
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        int totalAssets = assets.Count;
        int activeAssets = 0;
        int idleAssets = 0;
        int inRepair = 0;

        foreach (var a in assets)
        {
            if (statuses.TryGetValue(a.AssetStatusId, out var code))
            {
                if (code == "ST-NEW" || code == "ST-ASG" || code == "ST-PRG") activeAssets++;
                else if (code == "ST-WFU") idleAssets++;
                else if (code == "ST-WFV") inRepair++;
            }
            else
            {
                activeAssets++; // default
            }
        }

        int expiringWarranty = assets.Count(x => x.WarrantyEnd.HasValue && x.WarrantyEnd.Value >= now && x.WarrantyEnd.Value <= sixtyDaysOut);
        int expiringAmc = assets.Count(x => x.AmcEnd.HasValue && x.AmcEnd.Value >= now && x.AmcEnd.Value <= sixtyDaysOut);

        // Ticket Stats
        var ticketQuery = db.ItTickets.Where(x => !x.IsDeleted);
        if (!IsITUser() && currentUser.BranchId.HasValue)
            ticketQuery = ticketQuery.Where(x => x.BranchId == currentUser.BranchId);

        var tickets = await ticketQuery
            .Select(x => new { x.Status, x.PriorityId, x.SlaBreached })
            .ToListAsync(ct);

        var priorities = await db.ItMasterDatas
            .Where(x => x.Type == "Priority" && !x.IsDeleted && x.IsActive)
            .ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        int totalTickets = tickets.Count;
        int openTickets = tickets.Count(x => x.Status != "Closed" && x.Status != "Resolved");
        int criticalTickets = 0;
        int slaBreached = tickets.Count(x => x.SlaBreached);

        foreach (var t in tickets)
        {
            if (priorities.TryGetValue(t.PriorityId, out var pCode) && pCode == "PRI-CRIT")
            {
                criticalTickets++;
            }
        }

        // Branch-wise Assets Grouping
        var branchSummary = await db.ItAssets.Where(x => !x.IsDeleted)
            .GroupBy(x => x.Branch.Name)
            .Select(g => new { name = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).Take(5).ToListAsync(ct);

        // Category breakdown
        var categorySummary = assets
            .GroupBy(x => categories.TryGetValue(x.CategoryId, out var name) ? name : "Other")
            .Select(g => new { name = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count).ToList();

        return Json(new {
            totalAssets,
            activeAssets,
            idleAssets,
            inRepair,
            expiringWarranty,
            expiringAmc,
            totalTickets,
            openTickets,
            criticalTickets,
            slaBreached,
            branchSummary,
            categorySummary
        });
    }

    // ── MASTER CONFIG Choice CRUD ────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetMasterTypes(CancellationToken ct)
    {
        // Static listing of all 34 Masters as requested in requirements
        var list = new[] {
            "Branch", "Location", "Department", "Employee", "Vendor",
            "AssetCategory", "AssetType", "AssetBrand", "AssetModel",
            "OperatingSystem", "ProcessorType", "RAMConfiguration", "StorageType",
            "SoftwareCategory", "SoftwareVendor", "LicenseType",
            "Priority", "Severity", "Impact", "TicketCategory", "TicketSubCategory",
            "RootCause", "ResolutionType", "IssueType", "Status", "SLAPolicy",
            "AMCProvider", "WarrantyProvider", "DisposalReason", "PurchaseType",
            "CostCenter", "ApprovalLevels", "UserRoles", "NotificationTemplates"
        };
        return Json(list.OrderBy(x => x));
    }

    [HttpGet]
    public async Task<IActionResult> GetMasterData(string type, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(type)) return BadRequest(new { message = "Type is required." });
        var list = await db.ItMasterDatas
            .Where(x => x.Type == type && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Type, x.Code, x.Name, x.IsActive, x.SortOrder, x.Description })
            .ToListAsync(ct);
        return Json(list);
    }

    [HttpPost]
    public async Task<IActionResult> SaveMasterItem([FromBody] ItMasterData model, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Type)) return BadRequest(new { message = "Type is required." });
        if (string.IsNullOrWhiteSpace(model.Code)) return BadRequest(new { message = "Code is required." });
        if (string.IsNullOrWhiteSpace(model.Name)) return BadRequest(new { message = "Name is required." });

        if (model.Id == 0)
        {
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.ItMasterDatas.Add(model);
        }
        else
        {
            var existing = await db.ItMasterDatas.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "Master item not found." });

            existing.Code = model.Code;
            existing.Name = model.Name;
            existing.IsActive = model.IsActive;
            existing.SortOrder = model.SortOrder;
            existing.Description = model.Description;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Master item saved successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMasterItem(int id, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        var existing = await db.ItMasterDatas.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "Master item not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);

        return Json(new { ok = true, message = "Master item deleted successfully." });
    }

    // ── SOFTWARE LICENSES CRUD ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetSoftwareLicenses(string? search, CancellationToken ct)
    {
        var q = db.ItSoftwareLicenses.Include(x => x.Asset).Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(x => x.SoftwareName.Contains(search) || x.LicenseKey.Contains(search));
        }

        var items = await q.OrderBy(x => x.SoftwareName)
            .Select(x => new {
                x.Id, x.SoftwareName, x.VendorId, x.LicenseKey, x.Version,
                x.TotalLicenses, x.LicenseTypeId, x.IsActive, x.AssetId,
                deviceName = x.Asset != null ? x.Asset.Name : string.Empty,
                deviceCode = x.Asset != null ? x.Asset.AssetCode : string.Empty,
                installationDate = x.InstallationDate.ToString("yyyy-MM-dd"),
                expiryDate = x.ExpiryDate.HasValue ? x.ExpiryDate.Value.ToString("yyyy-MM-dd") : string.Empty
            }).ToListAsync(ct);

        return Json(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSoftwareLicense([FromBody] ItSoftwareLicense model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.SoftwareName)) return BadRequest(new { message = "Software name is required." });
        if (string.IsNullOrWhiteSpace(model.LicenseKey)) return BadRequest(new { message = "License key is required." });

        if (model.Id == 0)
        {
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.ItSoftwareLicenses.Add(model);
        }
        else
        {
            var existing = await db.ItSoftwareLicenses.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "License not found." });

            existing.SoftwareName = model.SoftwareName;
            existing.VendorId = model.VendorId;
            existing.LicenseKey = model.LicenseKey;
            existing.Version = model.Version;
            existing.InstallationDate = model.InstallationDate;
            existing.ExpiryDate = model.ExpiryDate;
            existing.AssetId = model.AssetId;
            existing.TotalLicenses = model.TotalLicenses;
            existing.LicenseTypeId = model.LicenseTypeId;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Software license saved successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSoftwareLicense(int id, CancellationToken ct)
    {
        var existing = await db.ItSoftwareLicenses.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "License not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Software license deleted." });
    }

    // ── KNOWLEDGE BASE CRUD ──────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetKbArticles(string? search, string? category, CancellationToken ct)
    {
        var q = db.ItKbArticles.Where(x => !x.IsDeleted);
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(x => x.Title.Contains(search) || x.Content.Contains(search) || x.Tags.Contains(search));
        }

        var items = await q.OrderByDescending(x => x.ViewsCount).ThenBy(x => x.Title)
            .Select(x => new {
                x.Id, x.Title, x.Content, x.CategoryId, x.Tags, x.ViewsCount,
                createdAt = x.CreatedAt.ToString("dd MMM yyyy")
            }).ToListAsync(ct);

        return Json(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveKbArticle([FromBody] ItKbArticle model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Title)) return BadRequest(new { message = "Article title is required." });
        if (string.IsNullOrWhiteSpace(model.Content)) return BadRequest(new { message = "Article content is required." });

        if (model.Id == 0)
        {
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.ItKbArticles.Add(model);
        }
        else
        {
            var existing = await db.ItKbArticles.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "Article not found." });

            existing.Title = model.Title;
            existing.Content = model.Content;
            existing.CategoryId = model.CategoryId;
            existing.Tags = model.Tags;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Knowledge Base article saved successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> IncrementKbViews(int id, CancellationToken ct)
    {
        var article = await db.ItKbArticles.FindAsync(new object[] { id }, ct);
        if (article == null || article.IsDeleted) return NotFound();
        article.ViewsCount++;
        await db.SaveChangesAsync(ct);
        return Json(new { ok = true });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteKbArticle(int id, CancellationToken ct)
    {
        var existing = await db.ItKbArticles.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "Article not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Article deleted." });
    }

    // ── PREVENTIVE MAINTENANCE CRUD ──────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetMaintenanceSchedules(CancellationToken ct)
    {
        var q = db.ItMaintenanceSchedules.Include(x => x.Asset).Where(x => !x.IsDeleted);
        if (!IsITUser() && currentUser.BranchId.HasValue)
            q = q.Where(x => x.Asset.BranchId == currentUser.BranchId);

        var items = await q.OrderBy(x => x.NextDueDate)
            .Select(x => new {
                x.Id, x.AssetId, x.Frequency, x.AssignedEngineer, x.ChecklistJson, x.Status,
                assetCode = x.Asset.AssetCode, assetName = x.Asset.Name,
                lastDoneDate = x.LastDoneDate.ToString("yyyy-MM-dd"),
                nextDueDate = x.NextDueDate.ToString("yyyy-MM-dd")
            }).ToListAsync(ct);

        return Json(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveMaintenanceSchedule([FromBody] ItMaintenanceSchedule model, CancellationToken ct)
    {
        if (model.AssetId <= 0) return BadRequest(new { message = "Asset is required." });

        if (model.Id == 0)
        {
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.ItMaintenanceSchedules.Add(model);
        }
        else
        {
            var existing = await db.ItMaintenanceSchedules.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "Maintenance schedule not found." });

            existing.AssetId = model.AssetId;
            existing.Frequency = model.Frequency;
            existing.LastDoneDate = model.LastDoneDate;
            existing.NextDueDate = model.NextDueDate;
            existing.AssignedEngineer = model.AssignedEngineer;
            existing.ChecklistJson = model.ChecklistJson;
            existing.Status = model.Status;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Maintenance schedule saved successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteMaintenanceSchedule(int id, CancellationToken ct)
    {
        var existing = await db.ItMaintenanceSchedules.FindAsync(new object[] { id }, ct);
        if (existing == null) return NotFound(new { message = "Schedule not found." });

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = currentUser.UserName;
        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Schedule deleted." });
    }

    // ── SLA POLICIES CRUD ────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetSlaPolicies(CancellationToken ct)
    {
        var items = await db.ItSlaPolicies.Where(x => !x.IsDeleted)
            .Select(x => new {
                x.Id, x.Name, x.PriorityId, x.ResponseTimeHours, x.ResolutionTimeHours, x.IsActive
            }).ToListAsync(ct);
        return Json(items);
    }

    [HttpPost]
    public async Task<IActionResult> SaveSlaPolicy([FromBody] ItSlaPolicy model, CancellationToken ct)
    {
        if (!IsAdmin()) return Forbid();
        if (string.IsNullOrWhiteSpace(model.Name)) return BadRequest(new { message = "Policy name is required." });

        if (model.Id == 0)
        {
            model.CreatedBy = currentUser.UserName;
            model.CreatedAt = DateTime.UtcNow;
            db.ItSlaPolicies.Add(model);
        }
        else
        {
            var existing = await db.ItSlaPolicies.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "Policy not found." });

            existing.Name = model.Name;
            existing.PriorityId = model.PriorityId;
            existing.ResponseTimeHours = model.ResponseTimeHours;
            existing.ResolutionTimeHours = model.ResolutionTimeHours;
            existing.IsActive = model.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "SLA policy saved successfully." });
    }
}
