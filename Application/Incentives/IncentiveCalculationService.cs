using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IIncentiveCalculationService
{
    Task<IReadOnlyList<CalculationResult>> CalculateMonthAsync(
        int month,
        int year,
        bool forceRecalculate = false,
        IReadOnlyList<IncentivePortal.DTOs.BranchCalcRule>? branchRules = null,
        bool applyOutstandingDeduction = false,
        IReadOnlyList<IncentivePortal.DTOs.PartyMappingRule>? customMappings = null,
        string? governorFiltersJson = null,
        CancellationToken cancellationToken = default);
}

public sealed class IncentiveCalculationService(
    IncentiveDbContext db,
    IDistributedLockService lockService,
    IAnalyticsRefreshService analyticsService,
    ILogger<IncentiveCalculationService> logger
) : IIncentiveCalculationService
{
    public class GovernorFilters
    {
        public int? BranchId { get; set; }
        public string? PartCategoryCode { get; set; }
        public string? PartyType { get; set; }
        public string? DealerCode { get; set; }
        public string? PartyCode { get; set; }
    }

    public async Task<IReadOnlyList<CalculationResult>> CalculateMonthAsync(
        int month,
        int year,
        bool forceRecalculate = false,
        IReadOnlyList<IncentivePortal.DTOs.BranchCalcRule>? branchRules = null,
        bool applyOutstandingDeduction = false,
        IReadOnlyList<IncentivePortal.DTOs.PartyMappingRule>? customMappings = null,
        string? governorFiltersJson = null,
        CancellationToken cancellationToken = default)
    {
        db.DisableAuditLogs = true;
        
        var lockResource = $"IncentiveCalc_{year}_{month}";
        await using var distributedLock = await lockService.AcquireLockAsync(lockResource, cancellationToken);

        using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {

            var isLocked = await db.MonthLocks.AnyAsync(x => x.LockYear == year && (x.LockMonth == month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
            if (isLocked)
                throw new InvalidOperationException("Locked month cannot be recalculated.");

            var period = await db.IncentivePeriods
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month && !p.IsDeleted, cancellationToken);

            if (period != null)
            {
                if (period.LockedFlag)
                {
                    throw new InvalidOperationException($"The period {month:D2}/{year} is locked and cannot be recalculated.");
                }
                period.SourceType = "Dynamic";
                period.Status = "Calculated";
                db.Entry(period).State = EntityState.Modified;
            }
            else
            {
                period = new IncentivePeriod
                {
                    Year = year,
                    Month = month,
                    SourceType = "Dynamic",
                    Status = "Calculated",
                    LockedFlag = false
                };
                db.IncentivePeriods.Add(period);
            }
            await db.SaveChangesAsync(cancellationToken);

            // Fetch existing incentives to preserve finalized records
            var existingIncentives = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            if (forceRecalculate)
            {
                var drafts = existingIncentives.Where(x => x.Status == "Draft").ToList();
                if (drafts.Count > 0)
                {
                    db.SsIncentives.RemoveRange(drafts);
                    await db.SaveChangesAsync(cancellationToken);
                    
                    // Remove them from our in-memory list
                    foreach(var d in drafts)
                    {
                        existingIncentives.Remove(d);
                    }
                }
            }

            var existingMap = existingIncentives
                .GroupBy(x => $"{x.PartyCode}_{x.PartCategoryCode}", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Fetch raw records
            var rawQuery = db.Raws.Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted);

            // Apply Governor Filters if provided
            if (!string.IsNullOrEmpty(governorFiltersJson))
            {
                var filters = JsonSerializer.Deserialize<GovernorFilters>(governorFiltersJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (filters != null)
                {
                    if (filters.BranchId.HasValue && filters.BranchId.Value > 0)
                    {
                        var branchCode = await db.Branches.Where(b => b.Id == filters.BranchId.Value).Select(b => b.Code).FirstOrDefaultAsync(cancellationToken);
                        if (!string.IsNullOrEmpty(branchCode))
                        {
                            rawQuery = rawQuery.Where(r => r.Loc == branchCode);
                        }
                    }
                    if (!string.IsNullOrEmpty(filters.PartCategoryCode))
                    {
                        rawQuery = rawQuery.Where(r => r.PartCategoryCode == filters.PartCategoryCode);
                    }
                    if (!string.IsNullOrEmpty(filters.PartyType))
                    {
                        rawQuery = rawQuery.Where(r => r.PartyType == filters.PartyType);
                    }
                    if (!string.IsNullOrEmpty(filters.DealerCode))
                    {
                        rawQuery = rawQuery.Where(r => r.DealerCode == filters.DealerCode);
                    }
                    if (!string.IsNullOrEmpty(filters.PartyCode))
                    {
                        rawQuery = rawQuery.Where(r => r.ConsPartyCode == filters.PartyCode || r.OriginalCode == filters.PartyCode);
                    }
                }
            }

            // Step 1: SQL Pushdown Aggregation
            var aggregatedData = await (from r in rawQuery
                let rawCode = r.OriginalCode ?? r.ConsPartyCode ?? ""
                join m in db.PartyCodeMappings.Where(x => x.IsActive && !x.IsDeleted) on rawCode equals m.AlternativeCode into mGroup
                from m in mGroup.DefaultIfEmpty()
                let mappedCode = m != null ? m.OriginalCode : rawCode
                
                join p in db.Parties on mappedCode equals p.PartyCode into pGroup
                from p in pGroup.DefaultIfEmpty()
                let resolvedCode = (p != null && p.OriginalPartyCode != null && p.OriginalPartyCode != "") ? p.OriginalPartyCode : mappedCode
                
                group r by new {
                   ResolvedCode = resolvedCode,
                   SaleLoc = r.Loc,
                   Category = r.PartCategoryCode,
                   PartyType = p != null ? p.DealerType : (r.PartyType ?? "INDEPENDENT WORKSHOP")
                } into g
                select new {
                   ResolvedCode = g.Key.ResolvedCode,
                   SaleLoc = g.Key.SaleLoc,
                   Category = g.Key.Category,
                   PartyType = g.Key.PartyType,
                   NetRetailSelling = g.Sum(x => x.NetRetailSelling),
                   DiscountAmount = g.Sum(x => x.DiscountAmount),
                   NetRetailQty = g.Sum(x => x.NetRetailQty ?? 0),
                   UniqueInvoices = g.Select(x => x.DocumentNum).Distinct().Count()
                }).ToListAsync(cancellationToken);

            if (aggregatedData.Count == 0)
            {
                return Array.Empty<CalculationResult>();
            }

            var activePeriodLocks = await db.IncentivePeriodLocks
                .Where(x => x.Year == year && x.Month == month && x.LockStatus == "Locked" && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            if (activePeriodLocks.Count > 0)
            {
                var processedPeriods = aggregatedData
                    .Select(r => new { Loc = r.SaleLoc ?? "", Category = r.Category ?? "Other" })
                    .Distinct()
                    .ToList();

                foreach (var item in processedPeriods)
                {
                    var lockRecord = activePeriodLocks.FirstOrDefault(x => 
                        x.BranchCode.Equals(item.Loc, StringComparison.OrdinalIgnoreCase) && 
                        x.PartCategoryCode.Equals(item.Category, StringComparison.OrdinalIgnoreCase));

                    if (lockRecord != null)
                    {
                        if (lockRecord.IncentiveSource == "Manual Upload")
                        {
                            throw new InvalidOperationException("This period has already been processed through Manual Upload and is locked. Incentive calculation cannot proceed unless an authorized user unlocks the period.");
                        }
                        else
                        {
                            throw new InvalidOperationException($"This period has already been processed through the Incentive Calculator and is locked for Branch {item.Loc} and Category {item.Category}. Incentive calculation cannot proceed unless an authorized user unlocks the period.");
                        }
                    }
                }
            }

            // Load ONLY the subset of Parties involved this month
            var resolvedCodes = aggregatedData.Select(x => x.ResolvedCode).Distinct().ToList();
            var parties = await db.Parties
                .IgnoreQueryFilters()
                .Include(x => x.Branch)
                .Where(x => resolvedCodes.Contains(x.PartyCode))
                .ToListAsync(cancellationToken);
            var partiesDict = parties.ToDictionary(p => p.PartyCode, StringComparer.OrdinalIgnoreCase);

            var ruleDict = branchRules != null 
                ? branchRules.ToDictionary(x => x.Location, StringComparer.OrdinalIgnoreCase) 
                : new Dictionary<string, IncentivePortal.DTOs.BranchCalcRule>(StringComparer.OrdinalIgnoreCase);

            // In-Memory Rule Application & Final Aggregation
            var grouped = aggregatedData
                .GroupBy(x => x.ResolvedCode)
                .Select(g => {
                    var partyCode = g.Key;
                    partiesDict.TryGetValue(partyCode, out var pObj);
                    var partyName = pObj?.PartyName ?? "Unknown Party";
                    var branchName = pObj?.Branch?.Name ?? "";
                    var loc = pObj?.Branch?.Code ?? "";
                    var partyType = pObj?.DealerType ?? "INDEPENDENT WORKSHOP";
                    
                    var validItemList = g.Where(x => {
                        var saleLoc = x.SaleLoc ?? "";
                        
                        // If branchRules were provided, and this sale's location is NOT in the rules, exclude the sale
                        if (branchRules != null && branchRules.Count > 0 && !ruleDict.ContainsKey(saleLoc))
                            return false;
                            
                        if (ruleDict.TryGetValue(saleLoc, out var rule))
                        {
                            // Enforce Category Rule
                            if (!string.IsNullOrEmpty(rule.AllowedCategories))
                            {
                                var allowedCats = rule.AllowedCategories.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                                if (!allowedCats.Contains(x.Category ?? "Other")) return false;
                            }
                            // Enforce PartyType Rule
                            if (!string.IsNullOrEmpty(rule.AllowedPartyTypes))
                            {
                                var allowedTypes = rule.AllowedPartyTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(pt => pt.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                                if (!allowedTypes.Contains(x.PartyType)) return false;
                            }
                        }
                        
                        return true;
                    }).ToList();
                    
                    return new {
                        FiscalYear = $"{year}-{((year + 1) % 100):D2}",
                        MonthNumber = month,
                        PartyCode = partyCode,
                        PartyName = partyName,
                        PartCategoryCode = "COMBINED",
                        PartyType = partyType,
                        Loc = loc,
                        BranchName = branchName,
                        TotalNetRetailSelling = validItemList.Sum(x => x.NetRetailSelling),
                        TotalDiscountAmount = validItemList.Sum(x => x.DiscountAmount),
                        TotalQuantity = validItemList.Sum(x => x.NetRetailQty),
                        TotalInvoiceCount = validItemList.Sum(x => x.UniqueInvoices)
                    };
                })
                .Where(x => x.TotalNetRetailSelling > 0 || x.TotalQuantity > 0)
                .ToList();

            var activeScheme = await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.SchemeYear == year && x.SchemeMonth == month)
                .ThenByDescending(x => x.SchemeYear)
                .ThenByDescending(x => x.SchemeMonth)
                .FirstOrDefaultAsync(cancellationToken);

            var activeTdsRules = await db.TdsRules
                .Where(x => !x.IsDeleted && x.EffectiveFrom <= new DateTime(year, month, 1) && x.EffectiveTo >= new DateTime(year, month, 1))
                .OrderByDescending(x => x.AnnualThreshold)
                .ToListAsync(cancellationToken);

            var activeOutstandingRules = await db.OutstandingRules
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.Priority)
                .ToListAsync(cancellationToken);

            var results = new List<CalculationResult>();
            var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in grouped)
            {
                var key = $"{group.PartyCode}_{group.PartCategoryCode}";
                processedKeys.Add(key);

                partiesDict.TryGetValue(group.PartyCode, out var targetParty);
                if (targetParty == null) continue;

                // Check if existing record is already Approved / Posted (non-Draft)
                if (existingMap.TryGetValue(key, out var existingInc) && existingInc.Status != "Draft")
                {
                    results.Add(new CalculationResult(group.PartyCode, existingInc.GrossIncentive, existingInc.Outstanding, existingInc.NetTransferAmount));
                    continue;
                }

                // Determine Incentive Type & Slab
                var isFixedIncentive = targetParty.DealerType == "Fixed Incentive" || targetParty.FixedIncentivePercent > 0;
                string incentiveType = "Slab";
                string applicableSlab = "";
                decimal incentivePercent = 0m;

                if (isFixedIncentive)
                {
                    incentiveType = "Fixed";
                    incentivePercent = targetParty.FixedIncentivePercent;
                    applicableSlab = "Fixed Payout Rate";
                }
                else
                {
                    incentiveType = "Slab";
                    var val = group.TotalNetRetailSelling;
                    var matchedSlab = activeScheme?.Details
                        .Where(d => val >= d.MinAchievementPercent && val <= d.MaxAchievementPercent)
                        .FirstOrDefault();

                    if (matchedSlab != null)
                    {
                        incentivePercent = matchedSlab.Percentage ?? 0m;
                        applicableSlab = $"₹{matchedSlab.MinAchievementPercent:N0}–{matchedSlab.MaxAchievementPercent:N0}";
                    }
                    else
                    {
                        if (val < 30000m)
                        {
                            incentivePercent = 0m;
                            applicableSlab = "₹0–29,999";
                        }
                        else if (val < 50000m)
                        {
                            incentivePercent = 3m;
                            applicableSlab = "₹30,000–49,999";
                        }
                        else if (val < 100000m)
                        {
                            incentivePercent = 5m;
                            applicableSlab = "₹50,000–99,999";
                        }
                        else
                        {
                            incentivePercent = 8m;
                            applicableSlab = "₹100,000+";
                        }
                    }
                }

                // Calculate Gross & Net Incentive
                decimal grossIncentive = Math.Round(group.TotalNetRetailSelling * (incentivePercent / 100m), 2);
                decimal netIncentive = Math.Max(0m, grossIncentive - group.TotalDiscountAmount);

                // Calculate TDS
                var bankDetails = await db.BankDetails.FirstOrDefaultAsync(b => b.PartyId == targetParty.Id && b.ApprovalStatus == "Approved" && !b.IsDeleted, cancellationToken);
                var hasPan = bankDetails != null && !string.IsNullOrWhiteSpace(bankDetails.PAN);
                
                decimal tdsRate = hasPan ? 0.10m : 0.20m;
                decimal tdsAmount = Math.Round(netIncentive * tdsRate, 2);

                // Outstanding balance
                var outstandingRecord = await db.DealerOutstandings
                    .FirstOrDefaultAsync(o => o.Year == year && o.Month == month && o.PartyCode == group.PartyCode && !o.IsDeleted, cancellationToken);
                decimal outstanding = outstandingRecord?.Outstanding ?? 0m;

                decimal netTransfer = Math.Max(0m, netIncentive - tdsAmount);
                if (applyOutstandingDeduction)
                {
                    netTransfer = Math.Max(0m, netTransfer - outstanding);
                }

                if (existingInc != null)
                {
                    // Update existing Draft
                    existingInc.SaleValue = group.TotalNetRetailSelling;
                    existingInc.OnBillDiscount = group.TotalDiscountAmount;
                    existingInc.GrossIncentive = grossIncentive;
                    existingInc.TdsAmount = tdsAmount;
                    existingInc.NetTransferAmount = netIncentive; // store Net Incentive as NetTransferAmount for calculation register columns
                    existingInc.Outstanding = outstanding;
                    existingInc.SlabPercent = incentivePercent / 100m;
                    existingInc.AchievementPercent = incentivePercent;
                    existingInc.ProcessingDate = DateTime.UtcNow;
                    existingInc.PartCategoryCode = group.PartCategoryCode;
                    existingInc.SourceLocation = group.Loc;
                    existingInc.IncentiveType = incentiveType;
                    existingInc.ApplicableSlab = applicableSlab;
                    existingInc.Status = "Draft"; // keeps in draft until manually posted
                    db.Entry(existingInc).State = EntityState.Modified;
                }
                else
                {
                    // Create new
                    var newInc = new SsIncentive
                    {
                        Month = month,
                        Year = year,
                        MonthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy"),
                        PartyCode = group.PartyCode,
                        PartyName = group.PartyName,
                        SaleValue = group.TotalNetRetailSelling,
                        OnBillDiscount = group.TotalDiscountAmount,
                        GrossIncentive = grossIncentive,
                        TdsAmount = tdsAmount,
                        NetTransferAmount = netIncentive, // Net Incentive
                        Outstanding = outstanding,
                        SlabPercent = incentivePercent / 100m,
                        AchievementPercent = incentivePercent,
                        ProcessingDate = DateTime.UtcNow,
                        PaymentStatus = "Pending",
                        BankAccountNumber = bankDetails?.AccountNumber ?? "-",
                        IFSC = bankDetails?.IFSC ?? "-",
                        BeneficiaryName = bankDetails?.AccountHolder ?? group.PartyName,
                        Mode = "Dynamic",
                        Status = "Draft",
                        PartCategoryCode = group.PartCategoryCode,
                        SourceLocation = group.Loc,
                        IncentiveType = incentiveType,
                        ApplicableSlab = applicableSlab
                    };
                    db.SsIncentives.Add(newInc);
                }

                results.Add(new CalculationResult(group.PartyCode, grossIncentive, outstanding, netIncentive));
            }

            await db.SaveChangesAsync(cancellationToken);

            // Clean up old unreferenced Draft records for this month/year
            var oldDrafts = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && x.Status == "Draft" && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var old in oldDrafts)
            {
                var key = $"{old.PartyCode}_{old.PartCategoryCode}";
                if (!processedKeys.Contains(key))
                {
                    old.IsDeleted = true;
                    db.Entry(old).State = EntityState.Modified;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await analyticsService.RefreshAsync(month, year, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return results;
        }
        finally
        {
            db.DisableAuditLogs = false;
        }
    }
}
