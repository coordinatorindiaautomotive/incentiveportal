using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Helpers;
using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace IncentivePortal.Controllers;

[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance},{AppRoles.SalesExecutive}")]
public sealed class IncentivesController(
    IncentiveDbContext db,
    ICurrentUser currentUser,
    IAuditEngineService auditService,
    IIncentiveCalculationService calculationService,
    IAnalyticsRefreshService analyticsService
) : Controller
{
    public IActionResult Verification()
    {
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetVerificationData(
        int? year,
        int? month,
        string? branch,
        string? category,
        string? status,
        string? search,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted);

        if (year.HasValue && year.Value > 0)
        {
            query = query.Where(x => x.Year == year.Value);
        }
        if (month.HasValue && month.Value > 0)
        {
            query = query.Where(x => x.Month == month.Value);
        }
        if (!string.IsNullOrEmpty(branch))
        {
            query = query.Where(x => x.SourceLocation == branch);
        }
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(x => x.PartCategoryCode == category);
        }
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(x => x.Status == status);
        }
        if (!string.IsNullOrEmpty(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.PartyCode.Contains(s) || x.PartyName.Contains(s));
        }

        // Calculate summary aggregates before pagination
        var totalSaleValue = await query.SumAsync(x => x.SaleValue, cancellationToken);
        var totalGrossIncentive = await query.SumAsync(x => x.GrossIncentive, cancellationToken);
        var totalNetTransfer = await query.SumAsync(x => x.NetTransferAmount, cancellationToken);
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ThenBy(x => x.PartyCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.Year,
                x.Month,
                x.MonthLabel,
                x.PartyCode,
                x.PartyName,
                x.SaleValue,
                x.SlabPercent,
                x.OnBillDiscount,
                x.AchievementPercent,
                x.GrossIncentive,
                x.TdsAmount,
                x.NetTransferAmount,
                x.Outstanding,
                x.PaymentStatus,
                x.PartCategoryCode,
                x.SourceLocation,
                x.Mode,
                x.Status,
                x.IsEdited,
                x.Remarks,
                x.ApprovedBy,
                approvedAt = x.ApprovedAt.HasValue ? x.ApprovedAt.Value.ToString("yyyy-MM-dd HH:mm") : "-",
                x.IncentiveType,
                x.ApplicableSlab
            })
            .ToListAsync(cancellationToken);

        return Json(new
        {
            items,
            totalCount,
            totalSaleValue,
            totalGrossIncentive,
            totalNetTransfer,
            page,
            pageSize
        });
    }

    [HttpPost]
    public async Task<IActionResult> ApproveRecords([FromBody] int[] ids, CancellationToken cancellationToken)
    {
        if (ids == null || ids.Length == 0)
            return BadRequest(new { message = "No records specified." });

        var records = await db.SsIncentives
            .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var user = currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        foreach (var rec in records)
        {
            // Verify if period is locked
            var isLocked = await db.IncentivePeriodLocks
                .AnyAsync(l => l.Year == rec.Year && l.Month == rec.Month 
                    && l.BranchCode == rec.SourceLocation && l.PartCategoryCode == rec.PartCategoryCode 
                    && l.LockStatus == "Locked" && !l.IsDeleted, cancellationToken);
            if (isLocked)
            {
                return BadRequest(new { message = $"Cannot approve. The period for Branch {rec.SourceLocation} and Category {rec.PartCategoryCode} is locked." });
            }

            var oldVal = JsonSerializer.Serialize(new { rec.Status, rec.ApprovedBy, rec.ApprovedAt });
            rec.Status = "Approved";
            rec.ApprovedBy = user;
            rec.ApprovedAt = now;
            rec.UpdatedAt = now;
            rec.UpdatedBy = user;
            db.Entry(rec).State = EntityState.Modified;

            var newVal = JsonSerializer.Serialize(new { rec.Status, rec.ApprovedBy, rec.ApprovedAt });
            await auditService.LogActionAsync("Approve", "SsIncentive", rec.Id.ToString(), oldVal, newVal, user, Request.HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = $"{records.Count} record(s) approved successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> RejectRecords([FromBody] RejectRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.Ids == null || request.Ids.Length == 0)
            return BadRequest(new { message = "No records specified." });
        if (string.IsNullOrWhiteSpace(request.Remarks))
            return BadRequest(new { message = "Rejection remarks are required." });

        var records = await db.SsIncentives
            .Where(x => request.Ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var user = currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        foreach (var rec in records)
        {
            // Verify if period is locked
            var isLocked = await db.IncentivePeriodLocks
                .AnyAsync(l => l.Year == rec.Year && l.Month == rec.Month 
                    && l.BranchCode == rec.SourceLocation && l.PartCategoryCode == rec.PartCategoryCode 
                    && l.LockStatus == "Locked" && !l.IsDeleted, cancellationToken);
            if (isLocked)
            {
                return BadRequest(new { message = $"Cannot reject. The period for Branch {rec.SourceLocation} and Category {rec.PartCategoryCode} is locked." });
            }

            var oldVal = JsonSerializer.Serialize(new { rec.Status, rec.Remarks });
            rec.Status = "Rejected";
            rec.Remarks = request.Remarks;
            rec.UpdatedAt = now;
            rec.UpdatedBy = user;
            db.Entry(rec).State = EntityState.Modified;

            var newVal = JsonSerializer.Serialize(new { rec.Status, rec.Remarks });
            await auditService.LogActionAsync("Reject", "SsIncentive", rec.Id.ToString(), oldVal, newVal, user, Request.HttpContext.Connection.RemoteIpAddress?.ToString());
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, message = $"{records.Count} record(s) rejected successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> OverrideRecord([FromBody] OverrideRequest request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { message = "Invalid override request." });
        if (string.IsNullOrWhiteSpace(request.Remarks))
            return BadRequest(new { message = "Mandatory override remarks are required." });

        var rec = await db.SsIncentives.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, cancellationToken);
        if (rec == null)
            return NotFound(new { message = "Incentive record not found." });

        // Verify if period is locked
        var isLocked = await db.IncentivePeriodLocks
            .AnyAsync(l => l.Year == rec.Year && l.Month == rec.Month 
                && l.BranchCode == rec.SourceLocation && l.PartCategoryCode == rec.PartCategoryCode 
                && l.LockStatus == "Locked" && !l.IsDeleted, cancellationToken);
        if (isLocked)
        {
            return BadRequest(new { message = $"Cannot override values. The period for Branch {rec.SourceLocation} and Category {rec.PartCategoryCode} is locked." });
        }

        var user = currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        var oldVal = JsonSerializer.Serialize(new 
        { 
            rec.SaleValue, 
            rec.OnBillDiscount, 
            rec.SlabPercent, 
            rec.GrossIncentive, 
            rec.TdsAmount,
            rec.NetTransferAmount, 
            rec.Remarks, 
            rec.IsEdited 
        });

        // Recalculate TDS on the override net incentive
        decimal grossIncentive = request.GrossIncentive;
        decimal netIncentive = Math.Max(0m, grossIncentive - request.Discount);
        
        // Find TDS rate from database active TDS rules or default
        var activeTdsRules = await db.TdsRules
            .Where(x => !x.IsDeleted && x.EffectiveFrom <= new DateTime(rec.Year, rec.Month, 1) && x.EffectiveTo >= new DateTime(rec.Year, rec.Month, 1))
            .OrderByDescending(x => x.AnnualThreshold)
            .ToListAsync(cancellationToken);

        var bankDetails = await db.BankDetails.FirstOrDefaultAsync(b => b.PartyId == db.Parties.FirstOrDefault(p => p.PartyCode == rec.PartyCode).Id && b.ApprovalStatus == "Approved" && !b.IsDeleted, cancellationToken);
        var hasPan = bankDetails != null && !string.IsNullOrWhiteSpace(bankDetails.PAN);

        TdsRule? matchedTdsRule = null;
        foreach (var rule in activeTdsRules)
        {
            if (netIncentive >= rule.AnnualThreshold)
            {
                matchedTdsRule = rule;
                break;
            }
        }
        decimal tdsRate = matchedTdsRule != null ? (hasPan ? matchedTdsRule.RateWithPan : matchedTdsRule.RateNoPan) : 0.05m;
        decimal tdsAmount = Math.Round(netIncentive * tdsRate, 2);
        decimal netTransfer = Math.Max(0m, netIncentive - tdsAmount - rec.Outstanding);

        rec.SaleValue = request.SaleValue;
        rec.OnBillDiscount = request.Discount;
        rec.SlabPercent = request.SlabPercent;
        rec.GrossIncentive = grossIncentive;
        rec.TdsAmount = tdsAmount;
        rec.NetTransferAmount = netTransfer;
        rec.Remarks = request.Remarks;
        rec.IsEdited = true;
        rec.UpdatedAt = now;
        rec.UpdatedBy = user;
        db.Entry(rec).State = EntityState.Modified;

        var newVal = JsonSerializer.Serialize(new 
        { 
            rec.SaleValue, 
            rec.OnBillDiscount, 
            rec.SlabPercent, 
            rec.GrossIncentive, 
            rec.TdsAmount,
            rec.NetTransferAmount, 
            rec.Remarks, 
            rec.IsEdited 
        });

        await auditService.LogActionAsync("Override", "SsIncentive", rec.Id.ToString(), oldVal, newVal, user, Request.HttpContext.Connection.RemoteIpAddress?.ToString());
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = "Incentive record overridden successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> RecalculateRecord([FromBody] int id, CancellationToken cancellationToken)
    {
        var rec = await db.SsIncentives.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
        if (rec == null)
            return NotFound(new { message = "Incentive record not found." });

        if (rec.Mode == "PreCalculated")
        {
            return BadRequest(new { message = "Cannot recalculate manual uploads. Please edit the values or re-upload the spreadsheet." });
        }

        // Verify if period is locked
        var isLocked = await db.IncentivePeriodLocks
            .AnyAsync(l => l.Year == rec.Year && l.Month == rec.Month 
                && l.BranchCode == rec.SourceLocation && l.PartCategoryCode == rec.PartCategoryCode 
                && l.LockStatus == "Locked" && !l.IsDeleted, cancellationToken);
        if (isLocked)
        {
            return BadRequest(new { message = $"Cannot recalculate. The period for Branch {rec.SourceLocation} and Category {rec.PartCategoryCode} is locked." });
        }

        var user = currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        var oldVal = JsonSerializer.Serialize(new { rec.SaleValue, rec.OnBillDiscount, rec.SlabPercent, rec.GrossIncentive, rec.NetTransferAmount, rec.Status });

        // Perform calculation using IncentiveCalculationService
        var result = await calculationService.CalculateMonthAsync(
            rec.Month,
            rec.Year,
            forceRecalculate: true,
            branchRules: null,
            applyOutstandingDeduction: true,
            customMappings: null,
            governorFiltersJson: JsonSerializer.Serialize(new { BranchId = 0, PartCategoryCode = rec.PartCategoryCode, PartyCode = rec.PartyCode }),
            cancellationToken: cancellationToken
        );

        if (result.Count == 0)
        {
            return BadRequest(new { message = "Recalculation yielded no results for this distributor code." });
        }

        // Reload the record from the database because CalculateMonthAsync has saved the fresh calculations
        await db.Entry(rec).ReloadAsync(cancellationToken);

        var newVal = JsonSerializer.Serialize(new { rec.SaleValue, rec.OnBillDiscount, rec.SlabPercent, rec.GrossIncentive, rec.NetTransferAmount, rec.Status });
        await auditService.LogActionAsync("Recalculate", "SsIncentive", rec.Id.ToString(), oldVal, newVal, user, Request.HttpContext.Connection.RemoteIpAddress?.ToString());

        return Json(new { ok = true, message = "Distributor incentives recalculated successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> PostRecords([FromBody] int[] ids, CancellationToken cancellationToken)
    {
        if (ids == null || ids.Length == 0)
            return BadRequest(new { message = "No records specified for posting." });

        var records = await db.SsIncentives
            .Where(x => ids.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var unapproved = records.Where(x => x.Status != "Approved").ToList();
        if (unapproved.Count > 0)
        {
            return BadRequest(new { message = "Only verified (Approved) records can be posted to the register. Please verify them first." });
        }

        var user = currentUser.UserName ?? "system";
        var now = DateTime.UtcNow;

        foreach (var rec in records)
        {
            var oldVal = JsonSerializer.Serialize(new { rec.Status });
            rec.Status = "Posted";
            rec.UpdatedAt = now;
            rec.UpdatedBy = user;
            db.Entry(rec).State = EntityState.Modified;

            var newVal = JsonSerializer.Serialize(new { rec.Status });
            await auditService.LogActionAsync("PostRegister", "SsIncentive", rec.Id.ToString(), oldVal, newVal, user, Request.HttpContext.Connection.RemoteIpAddress?.ToString());

            // Create/update period lock record for each posted branch/category pair
            var existingLock = await db.IncentivePeriodLocks
                .FirstOrDefaultAsync(x => x.Year == rec.Year && x.Month == rec.Month 
                    && x.BranchCode == rec.SourceLocation && x.PartCategoryCode == rec.PartCategoryCode && !x.IsDeleted, cancellationToken);
            if (existingLock != null)
            {
                existingLock.LockStatus = "Locked";
                existingLock.IncentiveSource = "Calculator";
                existingLock.PostedBy = user;
                existingLock.PostedDate = now;
                existingLock.LockedBy = user;
                existingLock.LockedDate = now;
                existingLock.UpdatedAt = now;
                existingLock.UpdatedBy = user;
                db.Entry(existingLock).State = EntityState.Modified;
            }
            else
            {
                var newLock = new IncentivePeriodLock
                {
                    Year = rec.Year,
                    Month = rec.Month,
                    BranchCode = rec.SourceLocation,
                    PartCategoryCode = rec.PartCategoryCode,
                    IncentiveSource = "Calculator",
                    LockStatus = "Locked",
                    PostedBy = user,
                    PostedDate = now,
                    LockedBy = user,
                    LockedDate = now,
                    CreatedBy = user,
                    CreatedAt = now
                };
                db.IncentivePeriodLocks.Add(newLock);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        if (records.Count > 0)
        {
            var first = records[0];
            await analyticsService.RefreshAsync(first.Month, first.Year, cancellationToken);
        }

        return Json(new { ok = true, message = $"{records.Count} records posted to final ledger and locked successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> GetSourceTransactions(int id, CancellationToken cancellationToken)
    {
        var incentive = await db.SsIncentives.FindAsync(new object[] { id }, cancellationToken);
        if (incentive == null)
            return NotFound(new { message = "Incentive record not found." });

        var query = db.Raws.AsNoTracking().Where(r => !r.IsDeleted);

        if (incentive.Mode == "PreCalculated")
        {
            query = query.Where(r => r.ImportLogId == incentive.ImportLogId 
                && r.PartCategoryCode == incentive.PartCategoryCode 
                && (r.OriginalCode == incentive.PartyCode || r.ConsPartyCode == incentive.PartyCode));
        }
        else
        {
            query = query.Where(r => r.YearNumber == incentive.Year 
                && r.MonthNumber == incentive.Month 
                && r.PartCategoryCode == incentive.PartCategoryCode 
                && (r.OriginalCode == incentive.PartyCode || r.ConsPartyCode == incentive.PartyCode));
        }

        var transactions = await query
            .OrderBy(r => r.RowNumber)
            .Select(r => new
            {
                r.RowNumber,
                documentNum = r.DocumentNum ?? "-",
                partNum = r.PartNum ?? "-",
                netRetailQty = r.NetRetailQty ?? 0,
                netRetailSelling = r.NetRetailSelling,
                discountAmount = r.DiscountAmount,
                remarks = r.Remarks ?? ""
            })
            .ToListAsync(cancellationToken);

        return Json(transactions);
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditHistory(int id, CancellationToken cancellationToken)
    {
        var idStr = id.ToString();
        var logs = await db.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == "SsIncentive" && l.EntityId == idStr)
            .OrderByDescending(l => l.ChangedAt)
            .Select(l => new
            {
                l.Action,
                l.ChangedBy,
                changedAt = l.ChangedAt.ToString("yyyy-MM-dd HH:mm"),
                l.OldValue,
                l.NewValue,
                l.IpAddress
            })
            .ToListAsync(cancellationToken);

        return Json(logs);
    }
}

public sealed class RejectRequest
{
    public int[] Ids { get; set; } = Array.Empty<int>();
    public string Remarks { get; set; } = string.Empty;
}

public sealed class OverrideRequest
{
    public int Id { get; set; }
    public decimal SaleValue { get; set; }
    public decimal Discount { get; set; }
    public decimal SlabPercent { get; set; }
    public decimal GrossIncentive { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
