using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

/// <summary>
/// Admin-only controller for browsing, filtering, and purging incentive data
/// across all related tables so that clean re-uploads become possible.
/// </summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class DataManagementController(
    IncentiveDbContext db,
    IDashboardService dashboardService) : Controller
{
    // =========================================================
    // INDEX — RENDER DATA MANAGEMENT PAGE
    // =========================================================
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var branches = await db.Branches
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Code)
            .ToListAsync(ct);

        var parties = await db.Parties
            .Where(p => !p.IsDeleted && p.Status == "Active")
            .OrderBy(p => p.PartyCode)
            .Select(p => new { p.PartyCode, p.PartyName })
            .ToListAsync(ct);

        ViewBag.Branches = branches;
        ViewBag.Parties = parties;
        return View();
    }

    // =========================================================
    // GET FILTER OPTIONS — AVAILABLE YEARS & MONTHS WITH DATA
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetFilterOptions(CancellationToken ct)
    {
        try
        {
            // Gather distinct years that have data
            var rawYears = await db.Raws.Where(r => r.YearNumber != null)
                .Select(r => r.YearNumber!.Value).Distinct().ToListAsync(ct);
            var ssYears = await db.SsIncentives.Select(s => s.Year).Distinct().ToListAsync(ct);

            var allYears = rawYears.Union(ssYears)
                .Distinct().OrderByDescending(y => y).ToList();

            return Json(new { years = allYears });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // SEARCH — COUNT RECORDS MATCHING FILTERS
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Search(
        int year,
        int month,
        string? location,
        string? partyCode,
        CancellationToken ct)
    {
        try
        {
            var loc = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            var pc = string.IsNullOrWhiteSpace(partyCode) ? null : partyCode.Trim();

            // ---------- Raw Records ----------
            var rawQ = db.Raws.Where(r => r.YearNumber == year);
            if (month > 0) rawQ = rawQ.Where(r => r.MonthNumber == month);
            if (loc != null) rawQ = rawQ.Where(r => r.Loc == loc);
            if (pc != null) rawQ = rawQ.Where(r => r.ConsPartyCode == pc);
            var rawCount = await rawQ.CountAsync(ct);

            // ---------- MonthlySales (Deleted) ----------
            var msCount = 0;

            // ---------- IncentiveCalculations (Deleted) ----------
            var calcCount = 0;

            // ---------- IncentiveLedgers (Deleted) ----------
            var ilCount = 0;

            // ---------- SsIncentives ----------
            var ssQ = db.SsIncentives.Where(s => s.Year == year);
            if (month > 0) ssQ = ssQ.Where(s => s.Month == month);
            if (pc != null) ssQ = ssQ.Where(s => s.PartyCode == pc);
            var piCount = await ssQ.CountAsync(ct);

            // ---------- CategorySalesAggregates ----------
            var csaQ = db.CategorySalesAggregates.Where(c => c.Year == year);
            if (month > 0) csaQ = csaQ.Where(c => c.Month == month);
            if (loc != null) csaQ = csaQ.Where(c => c.Loc == loc);
            var csaCount = await csaQ.CountAsync(ct);

            // ---------- DealerMonthlyPerformance ----------
            var dmpQ = db.DealerMonthlyPerformances.Include(d => d.Party).Where(d => d.Year == year);
            if (month > 0) dmpQ = dmpQ.Where(d => d.Month == month);
            if (pc != null) dmpQ = dmpQ.Where(d => d.Party.PartyCode == pc);
            var dmpCount = await dmpQ.CountAsync(ct);

            // ---------- IncentiveSummary ----------
            var isQ = db.IncentiveSummaries.Include(i => i.Party).Where(i => i.Year == year);
            if (month > 0) isQ = isQ.Where(i => i.Month == month);
            if (pc != null) isQ = isQ.Where(i => i.Party.PartyCode == pc);
            var isCount = await isQ.CountAsync(ct);

            // ---------- DealerSlabProgress ----------
            var dspQ = db.DealerSlabProgresses.Include(d => d.Party).Where(d => d.Year == year);
            if (month > 0) dspQ = dspQ.Where(d => d.Month == month);
            if (pc != null) dspQ = dspQ.Where(d => d.Party.PartyCode == pc);
            var dspCount = await dspQ.CountAsync(ct);

            // ---------- DealerGrowthAnalytics ----------
            var dgaQ = db.DealerGrowthAnalytics.Include(d => d.Party).Where(d => d.Year == year);
            if (month > 0) dgaQ = dgaQ.Where(d => d.Month == month);
            if (pc != null) dgaQ = dgaQ.Where(d => d.Party.PartyCode == pc);
            var dgaCount = await dgaQ.CountAsync(ct);

            // ---------- MonthLock status ----------
            bool isLocked = false;
            if (month > 0)
            {
                isLocked = await db.MonthLocks.AnyAsync(
                    m => m.LockYear == year && m.LockMonth == month && m.IsLocked, ct);
            }
            else
            {
                isLocked = await db.MonthLocks.AnyAsync(
                    m => m.LockYear == year && m.IsLocked, ct);
            }

            // totalCount excludes rawCount because Raw records are always kept untouched.
            var totalCount = piCount + csaCount + dmpCount + isCount + dspCount + dgaCount;

            // ---------- Sample preview rows from Raw (max 50) ----------
            var sampleRaw = await rawQ.OrderBy(r => r.Id).Take(50)
                .Select(r => new
                {
                    r.Id,
                    r.ConsPartyCode,
                    r.ConsPartyName,
                    r.Loc,
                    r.PartCategoryCode,
                    r.MonthNumber,
                    r.YearNumber,
                    r.NetRetailSelling,
                    r.DiscountAmount
                }).ToListAsync(ct);

            // ---------- Sample preview rows from SsIncentives (max 50) ----------
            var sampleSales = await db.SsIncentives
                .Where(s => s.Year == year)
                .Where(s => month <= 0 || s.Month == month)
                .Where(s => pc == null || s.PartyCode == pc)
                .OrderBy(s => s.Id).Take(50)
                .Select(s => new
                {
                    s.Id,
                    PartyCode = s.PartyCode,
                    PartyName = s.PartyName,
                    SourceLocation = s.PartCategoryCode, // Fallback mapped to PartCategoryCode
                    s.Month,
                    s.Year,
                    SaleValue = s.SaleValue,
                    Discount = s.OnBillDiscount,
                    ImportedSlabPercent = s.SlabPercent,
                    ImportedIncentive = s.GrossIncentive
                }).ToListAsync(ct);

            return Json(new
            {
                rawCount,
                msCount,
                calcCount,
                ilCount,
                piCount,
                csaCount,
                dmpCount,
                isCount,
                dspCount,
                dgaCount,
                totalCount,
                isLocked,
                sampleRaw,
                sampleSales
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // DELETE — CASCADING BULK DELETE WITH PROTECTION
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Delete(
        int year,
        int month,
        string? location,
        string? partyCode,
        CancellationToken ct)
    {
        try
        {
            var loc = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
            var pc = string.IsNullOrWhiteSpace(partyCode) ? null : partyCode.Trim();
            var userName = User.Identity?.Name ?? "system";

            // ── MonthLock protection ──
            if (month > 0)
            {
                var locked = await db.MonthLocks.AnyAsync(
                    m => m.LockYear == year && m.LockMonth == month && m.IsLocked, ct);
                if (locked)
                    return BadRequest(new { message = $"Period {month:D2}/{year} is locked. Please unlock it from Control Tower before deleting data." });
            }
            else
            {
                var locked = await db.MonthLocks.AnyAsync(
                    m => m.LockYear == year && m.IsLocked, ct);
                if (locked)
                    return BadRequest(new { message = $"One or more months in year {year} are locked. Please unlock them first." });
            }

            // Disable per-entity audit logs to avoid N×audit records for bulk delete
            db.DisableAuditLogs = true;

            int totalDeleted = 0;

            // ── 1. CategorySalesAggregates ──
            var csaQ = db.CategorySalesAggregates.IgnoreQueryFilters()
                .Where(c => c.Year == year);
            if (month > 0) csaQ = csaQ.Where(c => c.Month == month);
            if (loc != null) csaQ = csaQ.Where(c => c.Loc == loc);
            var csaList = await csaQ.ToListAsync(ct);
            if (csaList.Count > 0)
            {
                db.CategorySalesAggregates.RemoveRange(csaList);
                totalDeleted += csaList.Count;
            }

            // ── 2. DealerMonthlyPerformance ──
            var dmpQ = db.DealerMonthlyPerformances.IgnoreQueryFilters()
                .Include(d => d.Party)
                .Where(d => d.Year == year);
            if (month > 0) dmpQ = dmpQ.Where(d => d.Month == month);
            if (pc != null) dmpQ = dmpQ.Where(d => d.Party.PartyCode == pc);
            var dmpList = await dmpQ.ToListAsync(ct);
            if (dmpList.Count > 0)
            {
                db.DealerMonthlyPerformances.RemoveRange(dmpList);
                totalDeleted += dmpList.Count;
            }

            // ── 3. IncentiveSummary ──
            var isQ = db.IncentiveSummaries.IgnoreQueryFilters()
                .Include(i => i.Party)
                .Where(i => i.Year == year);
            if (month > 0) isQ = isQ.Where(i => i.Month == month);
            if (pc != null) isQ = isQ.Where(i => i.Party.PartyCode == pc);
            var isList = await isQ.ToListAsync(ct);
            if (isList.Count > 0)
            {
                db.IncentiveSummaries.RemoveRange(isList);
                totalDeleted += isList.Count;
            }

            // ── 4. DealerSlabProgress ──
            var dspQ = db.DealerSlabProgresses.IgnoreQueryFilters()
                .Include(d => d.Party)
                .Where(d => d.Year == year);
            if (month > 0) dspQ = dspQ.Where(d => d.Month == month);
            if (pc != null) dspQ = dspQ.Where(d => d.Party.PartyCode == pc);
            var dspList = await dspQ.ToListAsync(ct);
            if (dspList.Count > 0)
            {
                db.DealerSlabProgresses.RemoveRange(dspList);
                totalDeleted += dspList.Count;
            }

            // ── 5. DealerGrowthAnalytics ──
            var dgaQ = db.DealerGrowthAnalytics.IgnoreQueryFilters()
                .Include(d => d.Party)
                .Where(d => d.Year == year);
            if (month > 0) dgaQ = dgaQ.Where(d => d.Month == month);
            if (pc != null) dgaQ = dgaQ.Where(d => d.Party.PartyCode == pc);
            var dgaList = await dgaQ.ToListAsync(ct);
            if (dgaList.Count > 0)
            {
                db.DealerGrowthAnalytics.RemoveRange(dgaList);
                totalDeleted += dgaList.Count;
            }

            // ── 6. SsIncentives ──
            var ssDelQ = db.SsIncentives.IgnoreQueryFilters()
                .Where(s => s.Year == year);
            if (month > 0) ssDelQ = ssDelQ.Where(s => s.Month == month);
            if (pc != null) ssDelQ = ssDelQ.Where(s => s.PartyCode == pc);
            var ssDelList = await ssDelQ.ToListAsync(ct);
            if (ssDelList.Count > 0)
            {
                db.SsIncentives.RemoveRange(ssDelList);
                totalDeleted += ssDelList.Count;
            }

            // ── 7. ImportLogs (Delete logs when doing a full period purge) ──
            if (loc == null && pc == null)
            {
                var logQ = db.ImportLogs.Where(l => l.Year == year && (l.ImportType == "MonthlySales" || l.ImportType == "Raw"));
                if (month > 0) logQ = logQ.Where(l => l.Month == month);
                var logList = await logQ.ToListAsync(ct);
                if (logList.Count > 0)
                {
                    var logIds = logList.Select(l => l.Id).ToList();
                    var rawToUpdate = await db.Raws.Where(r => r.ImportLogId.HasValue && logIds.Contains(r.ImportLogId.Value)).ToListAsync(ct);
                    foreach (var raw in rawToUpdate)
                    {
                        raw.ImportLogId = null;
                    }
                    foreach (var log in logList)
                    {
                        log.PreviousImportLogId = null;
                    }
                    await db.SaveChangesAsync(ct);
                    db.ImportLogs.RemoveRange(logList);
                    totalDeleted += logList.Count;
                }
            }

            // ── 8. IncentivePeriods (Delete period metadata when doing a full period purge) ──
            if (loc == null && pc == null)
            {
                var periodQ = db.IncentivePeriods.Where(p => p.Year == year);
                if (month > 0) periodQ = periodQ.Where(p => p.Month == month);
                var periodList = await periodQ.ToListAsync(ct);
                if (periodList.Count > 0)
                {
                    db.IncentivePeriods.RemoveRange(periodList);
                    totalDeleted += periodList.Count;
                }
            }

            // ── Save all deletions ──
            await db.SaveChangesAsync(ct);

            // ── Manual audit log entry for the bulk operation ──
            db.DisableAuditLogs = false;
            var monthLabel = month > 0 ? $"{month:D2}/{year}" : $"All months of {year}";
            var scope = $"Year={year}, Month={monthLabel}";
            if (loc != null) scope += $", Location={loc}";
            if (pc != null) scope += $", PartyCode={pc}";

            db.AuditLogs.Add(new AuditLog
            {
                EntityName = "BulkDataDelete",
                EntityId = "0",
                Action = "BulkDelete",
                OldValue = System.Text.Json.JsonSerializer.Serialize(new { scope, totalDeleted }),
                NewValue = "{}",
                ChangedBy = userName,
                ChangedAt = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await db.SaveChangesAsync(ct);

            // ── Invalidate dashboard cache ──
            dashboardService.InvalidateCache();

            return Json(new
            {
                ok = true,
                totalDeleted,
                message = $"Successfully purged {totalDeleted:N0} records for {monthLabel}." +
                    (loc != null ? $" Location: {loc}." : "") +
                    (pc != null ? $" Party: {pc}." : "") +
                    " You can now re-upload fresh data."
            });
        }
        catch (Exception ex)
        {
            var fullMsg = ex.Message;
            if (ex.InnerException != null) fullMsg += " | " + ex.InnerException.Message;
            return BadRequest(new { message = fullMsg });
        }
    }
}
