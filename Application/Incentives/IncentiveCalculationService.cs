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

/// <summary>
/// Service interface for executing full monthly incentive calculations, dynamic TDS deductions, outstanding balance adjustments, and bank transfer generations.
/// </summary>
public interface IIncentiveCalculationService
{
    /// <summary>
    /// Executes monthly incentive calculation for all active dealers, including dynamic TDS and outstanding ledger adjustments.
    /// Optional branchRules restricts calculation to specific branches/categories/party-types.
    /// The Raw table is never modified by this method.
    /// </summary>
    Task<IReadOnlyList<CalculationResult>> CalculateMonthAsync(int month, int year, bool forceRecalculate = false, IReadOnlyList<IncentivePortal.DTOs.BranchCalcRule>? branchRules = null, bool applyOutstandingDeduction = false, IReadOnlyList<IncentivePortal.DTOs.PartyMappingRule>? customMappings = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sealed implementation of <see cref="IIncentiveCalculationService"/> managing transactional isolation and multi-step ledgers population.
/// </summary>
public sealed class IncentiveCalculationService(
    IncentiveDbContext db,
    IAnalyticsRefreshService analyticsService,
    IIncentiveEngineService incentiveEngine,
    ILogger<IncentiveCalculationService> logger
) : IIncentiveCalculationService
{
    public async Task<IReadOnlyList<CalculationResult>> CalculateMonthAsync(int month, int year, bool forceRecalculate = false, IReadOnlyList<IncentivePortal.DTOs.BranchCalcRule>? branchRules = null, bool applyOutstandingDeduction = false, IReadOnlyList<IncentivePortal.DTOs.PartyMappingRule>? customMappings = null, CancellationToken cancellationToken = default)
    {
        db.DisableAuditLogs = true;
        try
        {
            var isLocked = await db.MonthLocks.AnyAsync(x => x.LockYear == year && (x.LockMonth == month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
            if (isLocked)
                throw new InvalidOperationException("Locked month cannot be recalculated.");

            // Check if the latest import for this period was a pre-calculated file
            var latestLog = await db.ImportLogs
                .Where(x => x.Year == year && x.Month == month && !x.IsDeleted && !x.IsHistorical && !x.FileName.StartsWith("Recalculation_From_Raw") && x.FileName != "System_Generated_Placeholder")
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            bool isPreCalculated = latestLog != null && latestLog.ImportType == "MonthlySales";

            var period = await db.IncentivePeriods
                .FirstOrDefaultAsync(p => p.Year == year && p.Month == month && !p.IsDeleted, cancellationToken);

            if (period != null)
            {
                if (period.LockedFlag)
                {
                    throw new InvalidOperationException($"The period {month:D2}/{year} is locked and cannot be recalculated.");
                }
                
                if (isPreCalculated)
                {
                    if (period.SourceType == "PayoutImport")
                    {
                        throw new InvalidOperationException($"The period {month:D2}/{year} is owned by source type '{period.SourceType}' and cannot be calculated dynamically.");
                    }
                    period.SourceType = "PreCalculated";
                }
                else
                {
                    if (period.SourceType == "PreCalculated" || period.SourceType == "PayoutImport")
                    {
                        throw new InvalidOperationException($"The period {month:D2}/{year} is owned by source type '{period.SourceType}' and cannot be calculated dynamically.");
                    }
                    period.SourceType = "Dynamic";
                }
                
                period.Status = "Calculated";
                db.Entry(period).State = EntityState.Modified;
            }
            else
            {
                period = new IncentivePeriod
                {
                    Year = year,
                    Month = month,
                    SourceType = isPreCalculated ? "PreCalculated" : "Dynamic",
                    Status = "Calculated",
                    LockedFlag = false
                };
                db.IncentivePeriods.Add(period);
            }
            await db.SaveChangesAsync(cancellationToken);

            if (isPreCalculated)
            {
                // Precalculated files bypass calculations. Just run analytics refresh.
                await analyticsService.RefreshAsync(month, year, cancellationToken);
                return Array.Empty<CalculationResult>();
            }

            // Fetch existing incentives to preserve manual overrides, UTRs, and outstanding balance
            var existingIncentives = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && !x.IsDeleted)
                .ToDictionaryAsync(x => x.PartyCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // Fetch raw records
            var rawQuery = db.Raws.Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted);
            var hasRawData = await rawQuery.AnyAsync(cancellationToken);
            if (!hasRawData)
            {
                // No raw data to calculate.
                return Array.Empty<CalculationResult>();
            }

            var rawRecords = await rawQuery.ToListAsync(cancellationToken);

            // Load ALL parties including soft-deleted alternate/merged codes so their raw sales
            // are still attributed to the original party during calculation.
            var parties = await db.Parties
                .IgnoreQueryFilters()
                .Include(x => x.Branch)
                .ToListAsync(cancellationToken);
            var partiesDict = parties
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var dbBranches = await db.Branches.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var rulesLookup = branchRules?.ToDictionary(r => r.Location, StringComparer.OrdinalIgnoreCase) ?? new();

            // Group raw records by target (original) party after filtering with rules
            var aggregatedSales = new Dictionary<string, (decimal SaleValue, decimal Discount, string Loc, string Cat)>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in rawRecords)
            {
                var partyCode = !string.IsNullOrEmpty(r.OriginalCode) ? r.OriginalCode : r.ConsPartyCode ?? string.Empty;
                var customMappedCode = customMappings?.FirstOrDefault(m => string.Equals(m.AlternateCode, partyCode, StringComparison.OrdinalIgnoreCase))?.OriginalCode;
                partyCode = !string.IsNullOrEmpty(customMappedCode) ? customMappedCode : partyCode;
                if (!partiesDict.TryGetValue(partyCode, out var party)) continue;

                // Resolve to the original/parent party when this is an alternate code.
                // If the original party record doesn't exist in the dict yet (edge case), skip
                // the merge so the sale is still counted under the alternate's own code.
                var targetParty = party;
                if (!string.IsNullOrEmpty(party.OriginalPartyCode))
                {
                    if (partiesDict.TryGetValue(party.OriginalPartyCode, out var origParty))
                    {
                        targetParty = origParty;
                    }
                    // else: original party not found — keep targetParty as the alternate
                }

                var loc = r.Loc ?? string.Empty;
                var cat = r.PartCategoryCode ?? string.Empty;
                bool isAllowed = false;

                if (rulesLookup.TryGetValue(loc, out var customRule))
                {
                    var allowedCats = (customRule.AllowedCategories ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var allowedTypes = (customRule.AllowedPartyTypes ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    bool catOk = string.IsNullOrEmpty(cat) || allowedCats.Length == 0 || allowedCats.Any(c => c.Equals(cat, StringComparison.OrdinalIgnoreCase));
                    bool typeOk = IsPartyTypeAllowed(targetParty.DealerType, allowedTypes);
                    isAllowed = catOk && typeOk;
                }
                else if (dbBranches.TryGetValue(loc, out var br))
                {
                    var allowedCats = (br.AllowedCategories ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var allowedTypes = (br.AllowedPartyTypes ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    bool catOk = string.IsNullOrEmpty(cat) || allowedCats.Length == 0 || allowedCats.Any(c => c.Equals(cat, StringComparison.OrdinalIgnoreCase));
                    bool typeOk = IsPartyTypeAllowed(targetParty.DealerType, allowedTypes);
                    isAllowed = catOk && typeOk;
                }
                else
                {
                    isAllowed = true;
                }

                if (!isAllowed) continue;

                var targetCode = targetParty.PartyCode;
                if (aggregatedSales.TryGetValue(targetCode, out var val))
                {
                    val.SaleValue += r.NetRetailSelling;
                    val.Discount += r.DiscountAmount;
                    aggregatedSales[targetCode] = val;
                }
                else
                {
                    aggregatedSales[targetCode] = (r.NetRetailSelling, r.DiscountAmount, loc, cat);
                }
            }

            var activeTdsRules = await db.TdsRules
                .Where(x => !x.IsDeleted && x.EffectiveFrom <= new DateTime(year, month, 1) && x.EffectiveTo >= new DateTime(year, month, 1))
                .OrderByDescending(x => x.AnnualThreshold)
                .ToListAsync(cancellationToken);

            var activeOutstandingRules = await db.OutstandingRules
                .Where(x => x.IsActive && !x.IsDeleted)
                .OrderBy(x => x.Priority)
                .ToListAsync(cancellationToken);

            var partyCodesList = aggregatedSales.Keys.ToList();
            var priorAnnualIncentives = await db.SsIncentives
                .Where(x => partyCodesList.Contains(x.PartyCode) && x.Year == year && x.Month != month && !x.IsDeleted)
                .GroupBy(x => x.PartyCode)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(x => x.GrossIncentive), StringComparer.OrdinalIgnoreCase, cancellationToken);

            var scheme = await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => x.Name != "Imported Workbook Scheme" && x.EffectiveFrom <= new DateTime(year, month, 1) && x.EffectiveTo >= new DateTime(year, month, 1))
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

            var activePartyIds = partiesDict.Values.Select(p => p.Id).Distinct().ToList();
            var allBankDetails = await db.BankDetails
                .AsNoTracking()
                .Where(x => activePartyIds.Contains(x.PartyId) && x.ApprovalStatus == "Approved" && !x.IsDeleted)
                .OrderByDescending(x => x.IsPrimary)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);
            var bankDetailsDict = allBankDetails
                .GroupBy(x => x.PartyId)
                .ToDictionary(g => g.Key, g => g.First());

            var results = new List<CalculationResult>();
            var processedPartyCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var achievementPercents = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in aggregatedSales)
            {
                var targetPartyCode = kvp.Key;
                var aggregated = kvp.Value;

                if (!partiesDict.TryGetValue(targetPartyCode, out var targetParty)) continue;

                processedPartyCodes.Add(targetParty.PartyCode);

                // Fetch bank details for party
                bankDetailsDict.TryGetValue(targetParty.Id, out var bank);

                existingIncentives.TryGetValue(targetParty.PartyCode, out var ssIncentive);

                if (ssIncentive != null && ssIncentive.PaymentStatus != "Pending")
                {
                    // Locked / already matched payout — skip recalculating
                    results.Add(new CalculationResult(targetParty.PartyCode, ssIncentive.GrossIncentive, ssIncentive.Outstanding - ssIncentive.NetTransferAmount, ssIncentive.NetTransferAmount));
                    continue;
                }

                // Setup outstanding
                decimal outstanding = ssIncentive?.Outstanding ?? 0m;

                var isFixedIncentive = targetParty.DealerType == "Fixed Incentive";
                var targetDate = new DateTime(year, month, 1);
                
                // Fix 1: Validate active scheme for dynamic dealers
                bool hasValidScheme = isFixedIncentive || (scheme != null && scheme.EffectiveFrom <= targetDate && targetDate <= scheme.EffectiveTo && scheme.IsActive);

                var slabVal = Math.Max(0m, aggregated.SaleValue);
                var slab = isFixedIncentive ? null : (hasValidScheme ? scheme?.Details.FirstOrDefault(x => slabVal >= x.MinAchievementPercent && slabVal <= x.MaxAchievementPercent) : null);

                // Calculate gross
                decimal gross = 0m;
                if (hasValidScheme)
                {
                    var hasActiveDbRules = await db.RuleMasters.AnyAsync(r => r.IsActive && !r.IsDeleted && r.Versions.Any(v => v.IsActive && !v.IsDeleted && v.EffectiveFrom <= targetDate && v.EffectiveTo >= targetDate), cancellationToken);

                    // Prepare a temporary SsIncentive representation for rule engine call
                    var tempIncentive = new SsIncentive
                    {
                        Month = month,
                        Year = year,
                        PartyCode = targetParty.PartyCode,
                        PartyName = targetParty.PartyName,
                        SaleValue = aggregated.SaleValue,
                        OnBillDiscount = aggregated.Discount,
                        Outstanding = outstanding,
                        PartCategoryCode = aggregated.Cat,
                        SourceLocation = aggregated.Loc
                    };

                    if (hasActiveDbRules)
                    {
                        var additionalContext = new Dictionary<string, decimal>();
                        var growthRecord = await db.DealerGrowthAnalytics
                            .FirstOrDefaultAsync(g => g.PartyId == targetParty.Id && g.Month == month && g.Year == year, cancellationToken);
                        if (growthRecord != null)
                        {
                            additionalContext["Growth"] = growthRecord.SalesGrowthMoM * 100m;
                        }

                        gross = await incentiveEngine.CalculateGrossIncentiveAsync(tempIncentive, targetDate, additionalContext);
                    }
                    else
                    {
                        if (isFixedIncentive)
                        {
                            gross = Math.Round(Math.Max(0, aggregated.SaleValue * (targetParty.FixedIncentivePercent / 100m)), 0, MidpointRounding.AwayFromZero);
                        }
                        else if (slab != null)
                        {
                            gross = Math.Round(Math.Max(0, (slab.FixedAmount ?? 0m) + aggregated.SaleValue * ((slab.Percentage ?? 0m) / 100m)), 0, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            gross = 0m;
                        }
                    }

                    gross = Math.Max(0, gross - aggregated.Discount);
                }

                // TDS
                var hasPan = bank != null && !string.IsNullOrWhiteSpace(bank.PAN);
                priorAnnualIncentives.TryGetValue(targetParty.PartyCode, out var priorGross);
                decimal totalAnnualIncentive = priorGross + gross;

                TdsRule? matchedTdsRule = null;
                foreach (var rule in activeTdsRules)
                {
                    if (totalAnnualIncentive >= rule.AnnualThreshold)
                    {
                        matchedTdsRule = rule;
                        break;
                    }
                }

                // Fix 2: TDS fallback when no TdsRule is found
                decimal tdsPercent;
                string? tdsNote = null;
                if (matchedTdsRule != null)
                {
                    tdsPercent = hasPan ? (matchedTdsRule.RateWithPan * 100m) : (matchedTdsRule.RateNoPan * 100m);
                }
                else
                {
                    logger.LogWarning("No active TDS rule found for party {PartyCode}.", targetParty.PartyCode);
                    tdsPercent = 20m;
                    tdsNote = "Default 20% applied — no active TDS rule found";
                }

                var tds = Math.Round(gross * (tdsPercent / 100m), 0, MidpointRounding.AwayFromZero);
                var netEligible = Math.Max(0, gross - tds);

                // Outstanding Adjustments
                OutstandingRule? matchedOutstandingRule = null;
                foreach (var rule in activeOutstandingRules)
                {
                    if (outstanding >= rule.ThresholdAmount)
                    {
                        matchedOutstandingRule = rule;
                        break;
                    }
                }

                decimal deductionRate = matchedOutstandingRule != null ? matchedOutstandingRule.DeductionRate : 1.00m;
                decimal maxDeduction = netEligible * deductionRate;
                var adjusted = Math.Max(0m, Math.Min(maxDeduction, outstanding));
                var transfer = Math.Max(0, netEligible - adjusted);

                // Fix 4: Optional outstanding deduction directly from NetTransferAmount
                if (applyOutstandingDeduction)
                {
                    transfer = Math.Max(0, transfer - outstanding);
                }

                var achievementPercent = isFixedIncentive ? targetParty.FixedIncentivePercent : (slab?.Percentage ?? 0m);
                achievementPercents[targetParty.PartyCode] = achievementPercent;

                if (ssIncentive != null)
                {
                    // Update existing
                    ssIncentive.SaleValue = aggregated.SaleValue;
                    ssIncentive.OnBillDiscount = aggregated.Discount;
                    ssIncentive.GrossIncentive = gross;
                    ssIncentive.TdsAmount = tds;
                    ssIncentive.NetTransferAmount = transfer;
                    if (outstanding < 0)
                    {
                        ssIncentive.PaymentStatus = "Credit Party";
                    }
                    else if (transfer == 0)
                    {
                        ssIncentive.PaymentStatus = "Paid";
                        ssIncentive.PaymentDate = DateTime.UtcNow;
                    }
                    ssIncentive.SlabPercent = isFixedIncentive ? targetParty.FixedIncentivePercent / 100m : (slab?.Percentage ?? 0m) / 100m;
                    ssIncentive.AchievementPercent = achievementPercent;
                    ssIncentive.ProcessingDate = DateTime.UtcNow;
                    ssIncentive.BankAccountNumber = bank?.AccountNumber ?? "-";
                    ssIncentive.IFSC = bank?.IFSC ?? "-";
                    ssIncentive.BeneficiaryName = bank?.AccountHolder ?? targetParty.PartyName;
                    ssIncentive.Mode = "Dynamic";
                    ssIncentive.Status = hasValidScheme ? "Posted" : "NoActiveScheme";
                    ssIncentive.PartCategoryCode = aggregated.Cat;
                    ssIncentive.SourceLocation = aggregated.Loc;
                    ssIncentive.TdsNote = tdsNote;
                    db.Entry(ssIncentive).State = EntityState.Modified;
                }
                else
                {
                    // Create new
                    ssIncentive = new SsIncentive
                    {
                        Month = month,
                        Year = year,
                        MonthLabel = new DateTime(year, month, 1).ToString("MMMM yyyy"),
                        PartyCode = targetParty.PartyCode,
                        PartyName = targetParty.PartyName,
                        SaleValue = aggregated.SaleValue,
                        OnBillDiscount = aggregated.Discount,
                        GrossIncentive = gross,
                        TdsAmount = tds,
                        NetTransferAmount = transfer,
                        Outstanding = outstanding,
                        SlabPercent = isFixedIncentive ? targetParty.FixedIncentivePercent / 100m : (slab?.Percentage ?? 0m) / 100m,
                        AchievementPercent = achievementPercent,
                        ProcessingDate = DateTime.UtcNow,
                        PaymentStatus = outstanding < 0 ? "Credit Party" : (transfer == 0 ? "Paid" : "Pending"),
                        PaymentDate = (outstanding >= 0 && transfer == 0) ? DateTime.UtcNow : (DateTime?)null,
                        BankAccountNumber = bank?.AccountNumber ?? "-",
                        IFSC = bank?.IFSC ?? "-",
                        BeneficiaryName = bank?.AccountHolder ?? targetParty.PartyName,
                        Mode = "Dynamic",
                        Status = hasValidScheme ? "Posted" : "NoActiveScheme",
                        PartCategoryCode = aggregated.Cat,
                        SourceLocation = aggregated.Loc,
                        TdsNote = tdsNote
                    };
                    db.SsIncentives.Add(ssIncentive);
                }

                results.Add(new CalculationResult(targetParty.PartyCode, gross, adjusted, transfer));
            }

            // Fix 3: Persist calculated AchievementPercent back to RawRecord (SalesData)
            foreach (var r in rawRecords)
            {
                var partyCode = r.ConsPartyCode ?? string.Empty;
                if (partiesDict.TryGetValue(partyCode, out var party))
                {
                    var targetCode = !string.IsNullOrEmpty(party.OriginalPartyCode) && partiesDict.TryGetValue(party.OriginalPartyCode, out var orig)
                        ? orig.PartyCode
                        : party.PartyCode;
                        
                    if (achievementPercents.TryGetValue(targetCode, out var achPct))
                    {
                        r.AchievementPercent = achPct;
                        db.Entry(r).State = EntityState.Modified;
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            // Clean up any SsIncentives that are no longer referenced, having no sales/incentive/outstanding
            var unusedIncentives = await db.SsIncentives
                .Where(x => x.Month == month && x.Year == year && !x.IsDeleted && !processedPartyCodes.Contains(x.PartyCode))
                .ToListAsync(cancellationToken);

            foreach (var unused in unusedIncentives)
            {
                if (unused.SaleValue == 0 && unused.GrossIncentive == 0 && unused.Outstanding == 0)
                {
                    unused.IsDeleted = true;
                    db.Entry(unused).State = EntityState.Modified;
                }
            }
            await db.SaveChangesAsync(cancellationToken);

            await analyticsService.RefreshAsync(month, year, cancellationToken);
            return results;
        }
        finally
        {
            db.DisableAuditLogs = false;
        }
    }

    private static bool IsPartyTypeAllowed(string? partyType, string[] allowedTypes)
    {
        if (allowedTypes.Length == 0) return true;
        if (string.IsNullOrEmpty(partyType)) partyType = "INDEPENDENT WORKSHOP";

        var pt = partyType.Trim();
        
        if (pt.Equals("Fixed Incentive", StringComparison.OrdinalIgnoreCase) ||
            pt.Equals("Slab-Based", StringComparison.OrdinalIgnoreCase) ||
            pt.Equals("Dealer", StringComparison.OrdinalIgnoreCase))
        {
            pt = "INDEPENDENT WORKSHOP";
        }

        return allowedTypes.Any(t => t.Equals(pt, StringComparison.OrdinalIgnoreCase));
    }
}
