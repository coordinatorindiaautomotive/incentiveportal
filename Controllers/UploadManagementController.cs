using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Helpers;
using IncentivePortal.Data;
using IncentivePortal.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace IncentivePortal.Controllers;

/// <summary>
/// Controller for the dedicated Upload Management module and Period Lock management.
/// Access is restricted to Super Admin and HO Finance roles.
/// </summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class UploadManagementController(
    IncentiveDbContext db,
    ICurrentUser currentUser
) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetLocks(CancellationToken cancellationToken)
    {
        var locks = await db.MonthLocks
            .Where(x => x.IsLocked)
            .OrderByDescending(x => x.LockYear)
            .ThenByDescending(x => x.LockMonth)
            .Select(x => new
            {
                x.Id,
                x.LockYear,
                x.LockMonth,
                monthLabel = x.LockMonth == 0 ? "Full Year" : new DateTime(x.LockYear, x.LockMonth, 1).ToString("MMMM"),
                x.IsLocked,
                lockedAt = x.LockedAt.HasValue ? x.LockedAt.Value.ToString("yyyy-MM-dd HH:mm") : "-",
                x.LockedBy,
                x.UnlockReason
            })
            .ToListAsync(cancellationToken);

        return Json(locks);
    }

    [HttpPost]
    public async Task<IActionResult> LockPeriod(int year, int month, string? reason, CancellationToken cancellationToken)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Invalid year specified." });
        if (month < 0 || month > 12)
            return BadRequest(new { message = "Invalid month specified." });

        var exists = await db.MonthLocks.AnyAsync(x => x.LockYear == year && x.LockMonth == month && x.IsLocked, cancellationToken);
        if (exists)
            return BadRequest(new { message = "This period is already locked." });

        var newLock = new MonthLock
        {
            LockYear = year,
            LockMonth = month,
            IsLocked = true,
            LockedAt = DateTime.UtcNow,
            LockedBy = currentUser.UserName ?? "system",
            UnlockReason = reason ?? "Manually locked via Lock Manager"
        };

        db.MonthLocks.Add(newLock);
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = "Period locked successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> UnlockPeriod(int id, CancellationToken cancellationToken)
    {
        var lockRecord = await db.MonthLocks.FindAsync(new object[] { id }, cancellationToken);
        if (lockRecord == null)
            return NotFound(new { message = "Lock record not found." });

        db.MonthLocks.Remove(lockRecord);
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = "Period unlocked successfully." });
    }

    // ── Raw Data Deletion ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the count of Raw records for the given month/year so the UI can
    /// show "X records will be deleted" before the user confirms.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRawDataCount(int year, int month, CancellationToken cancellationToken)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Invalid year specified." });
        if (month < 1 || month > 12)
            return BadRequest(new { message = "Invalid month specified. Must be 1-12." });

        var count = await db.Raws
            .CountAsync(x => x.YearNumber == year && x.MonthNumber == month, cancellationToken);

        return Json(new { count, year, month });
    }

    /// <summary>
    /// Permanently deletes all Raw records for the given month/year.
    /// Also deletes any associated ImportLog entries for that period.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteRawData(int year, int month, string? reason, CancellationToken cancellationToken)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Invalid year specified." });
        if (month < 1 || month > 12)
            return BadRequest(new { message = "Invalid month specified. Must be 1-12." });

        // Count before deletion for the response summary
        var rawCount = await db.Raws
            .CountAsync(x => x.YearNumber == year && x.MonthNumber == month, cancellationToken);

        if (rawCount == 0)
            return BadRequest(new { message = $"No raw records found for {new DateTime(year, month, 1):MMMM yyyy}. Nothing to delete." });

        // Delete matching raw records in bulk
        await db.Raws
            .Where(x => x.YearNumber == year && x.MonthNumber == month)
            .ExecuteDeleteAsync(cancellationToken);

        // Also remove ImportLog entries for the same period so upload history is clean
        await db.ImportLogs
            .Where(x => x.Year == year && x.Month == month)
            .ExecuteDeleteAsync(cancellationToken);

        var monthName = new DateTime(year, month, 1).ToString("MMMM yyyy");
        var user = currentUser.UserName ?? "system";

        return Json(new
        {
            ok = true,
            deletedCount = rawCount,
            message = $"{rawCount:N0} raw record(s) for {monthName} deleted successfully by {user}."
        });
    }

    // ── Granular Incentive Period Locks ─────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetIncentivePeriodLocks(CancellationToken cancellationToken)
    {
        var locks = await db.IncentivePeriodLocks
            .Where(x => !x.IsDeleted)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .Select(x => new
            {
                x.Id,
                x.Year,
                x.Month,
                monthLabel = new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
                financialYear = (x.Month >= 4) ? $"FY {x.Year}-{((x.Year + 1) % 100):D2}" : $"FY {x.Year - 1}-{(x.Year % 100):D2}",
                x.BranchCode,
                x.PartCategoryCode,
                x.IncentiveSource,
                x.LockStatus,
                lockedDate = x.LockedDate.HasValue ? x.LockedDate.Value.ToString("yyyy-MM-dd HH:mm") : "-",
                x.LockedBy,
                postedDate = x.PostedDate.HasValue ? x.PostedDate.Value.ToString("yyyy-MM-dd HH:mm") : "-",
                x.PostedBy,
                x.UnlockReason,
                x.UnlockRemarks
            })
            .ToListAsync(cancellationToken);

        return Json(locks);
    }

    [HttpPost]
    public async Task<IActionResult> LockIncentivePeriod(
        int year,
        int month,
        string branchCode,
        string partCategoryCode,
        string incentiveSource,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { message = "Invalid year specified." });
        if (month < 1 || month > 12)
            return BadRequest(new { message = "Invalid month specified." });
        if (string.IsNullOrWhiteSpace(branchCode))
            return BadRequest(new { message = "Branch code is required." });
        if (string.IsNullOrWhiteSpace(partCategoryCode))
            return BadRequest(new { message = "Part category code is required." });
        if (string.IsNullOrWhiteSpace(incentiveSource))
            return BadRequest(new { message = "Incentive source is required." });

        var exists = await db.IncentivePeriodLocks
            .FirstOrDefaultAsync(x => x.Year == year && x.Month == month 
                && x.BranchCode == branchCode && x.PartCategoryCode == partCategoryCode && !x.IsDeleted, cancellationToken);

        if (exists != null)
        {
            if (exists.LockStatus == "Locked")
            {
                return BadRequest(new { message = "This period lock already exists and is active." });
            }
            else
            {
                exists.LockStatus = "Locked";
                exists.IncentiveSource = incentiveSource;
                exists.LockedBy = currentUser.UserName ?? "system";
                exists.LockedDate = DateTime.UtcNow;
                exists.UnlockReason = null;
                exists.UnlockRemarks = null;
                exists.UpdatedAt = DateTime.UtcNow;
                exists.UpdatedBy = currentUser.UserName ?? "system";
                db.Entry(exists).State = EntityState.Modified;
            }
        }
        else
        {
            var newLock = new IncentivePeriodLock
            {
                Year = year,
                Month = month,
                BranchCode = branchCode,
                PartCategoryCode = partCategoryCode,
                IncentiveSource = incentiveSource,
                LockStatus = "Locked",
                LockedBy = currentUser.UserName ?? "system",
                LockedDate = DateTime.UtcNow,
                CreatedBy = currentUser.UserName ?? "system",
                CreatedAt = DateTime.UtcNow
            };
            db.IncentivePeriodLocks.Add(newLock);
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = "Granular period lock applied successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> UnlockIncentivePeriod(
        int id,
        string reason,
        string remarks,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(AppRoles.SuperAdmin))
        {
            return StatusCode(403, new { message = "Access Denied: Only Super Admins can unlock period locks." });
        }

        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { message = "Unlock reason is required." });
        if (string.IsNullOrWhiteSpace(remarks))
            return BadRequest(new { message = "Unlock remarks/details are required." });

        var lockRecord = await db.IncentivePeriodLocks.FindAsync(new object[] { id }, cancellationToken);
        if (lockRecord == null || lockRecord.IsDeleted)
            return NotFound(new { message = "Lock record not found." });

        lockRecord.LockStatus = "Unlocked";
        lockRecord.UnlockReason = reason;
        lockRecord.UnlockRemarks = remarks;
        lockRecord.UpdatedAt = DateTime.UtcNow;
        lockRecord.UpdatedBy = currentUser.UserName ?? "system";

        db.Entry(lockRecord).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = "Granular period unlocked successfully." });
    }
}
