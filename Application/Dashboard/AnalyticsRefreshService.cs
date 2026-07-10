using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for compiling multi-dimensional analytical performance tables, slab progress metrics, and growth trajectories.
/// </summary>
public interface IAnalyticsRefreshService
{
    /// <summary>
    /// Processes monthly performance analytics, generating progress rates and growth trend triggers (MoM, YoY) for dashboard charts.
    /// </summary>
    Task RefreshAsync(int month, int year, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sealed implementation of <see cref="IAnalyticsRefreshService"/> that purges old analytics and generates fresh comparative reports.
/// </summary>
public sealed class AnalyticsRefreshService(IncentiveDbContext db, IDashboardService dashboardService, IPartyBranchMappingService branchMappingService) : IAnalyticsRefreshService
{
    public async Task RefreshAsync(int month, int year, CancellationToken cancellationToken = default)
    {
        db.DisableAuditLogs = true;
        try
        {
            // Check if latest import was a pre-calculated file
            var latestLog = await db.ImportLogs
                .Where(x => x.Year == year && x.Month == month && !x.IsHistorical)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            bool isPreCalculated = latestLog != null && latestLog.ImportType == "MonthlySales";

            // 1. Fetch current month SsIncentive records
            var currentIncentives = await db.SsIncentives
                .AsNoTracking()
                .Include(x => x.ImportLog)
                .Where(x => x.Month == month && x.Year == year && x.ImportLogId > 0 && !x.ImportLog!.IsHistorical && !x.IsDeleted && x.Status == "Posted")
                .ToListAsync(cancellationToken);

            if (currentIncentives.Count == 0) return;

            var parties = await db.Parties.AsNoTracking().ToDictionaryAsync(p => p.PartyCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // 2. Fetch scheme for progress tracking
            var calcDate = new DateTime(year, month, 1);
            var scheme = await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => x.Name != "Imported Workbook Scheme" && x.EffectiveFrom <= calcDate && x.EffectiveTo >= calcDate)
                .OrderByDescending(x => x.EffectiveFrom)
                .ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await db.IncentiveSchemes.Include(x => x.Details)
                    .Where(x => x.Name != "Imported Workbook Scheme" && x.EffectiveFrom <= calcDate)
                    .OrderByDescending(x => x.EffectiveFrom)
                    .ThenByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken)
                ?? await db.IncentiveSchemes.Include(x => x.Details)
                    .Where(x => x.Name != "Imported Workbook Scheme")
                    .OrderBy(x => x.EffectiveFrom)
                    .ThenByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken);

            // 3. Resolve scaling factor for partial month benchmarking
            var today = DateTime.Today;
            decimal scalingFactor = 1.0m;
            if (today.Month == month && today.Year == year)
            {
                int daysInMonth = DateTime.DaysInMonth(year, month);
                int currentDay = today.Day;
                scalingFactor = (decimal)currentDay / daysInMonth;
                if (scalingFactor < 0.01m) scalingFactor = 0.01m;
            }

            // 4. Fetch prior periods incentives
            int prevMonth = month == 1 ? 12 : month - 1;
            int prevYear = month == 1 ? year - 1 : year;
            var prevIncentivesRaw = await db.SsIncentives
                .AsNoTracking()
                .Where(x => x.Month == prevMonth && x.Year == prevYear && !x.IsDeleted && x.Status == "Posted")
                .ToListAsync(cancellationToken);
            var prevIncentives = prevIncentivesRaw
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new
                {
                    SaleValue = g.Sum(x => x.SaleValue),
                    GrossIncentive = g.Sum(x => x.GrossIncentive)
                }, StringComparer.OrdinalIgnoreCase);

            int lastYearMonth = month;
            int lastYearYear = year - 1;
            var lastYearIncentivesRaw = await db.SsIncentives
                .AsNoTracking()
                .Where(x => x.Month == lastYearMonth && x.Year == lastYearYear && !x.IsDeleted && x.Status == "Posted")
                .ToListAsync(cancellationToken);
            var lastYearIncentives = lastYearIncentivesRaw
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new
                {
                    SaleValue = g.Sum(x => x.SaleValue),
                    GrossIncentive = g.Sum(x => x.GrossIncentive)
                }, StringComparer.OrdinalIgnoreCase);

            // 5. Bulk delete existing records for the target period directly in the database
            await db.DealerMonthlyPerformances.Where(x => x.Month == month && x.Year == year).ExecuteDeleteAsync(cancellationToken);
            await db.IncentiveSummaries.Where(x => x.Month == month && x.Year == year).ExecuteDeleteAsync(cancellationToken);
            await db.DealerSlabProgresses.Where(x => x.Month == month && x.Year == year).ExecuteDeleteAsync(cancellationToken);
            await db.DealerGrowthAnalytics.Where(x => x.Month == month && x.Year == year).ExecuteDeleteAsync(cancellationToken);

            db.ChangeTracker.AutoDetectChangesEnabled = false;
            try
            {
                var groupedSales = currentIncentives
                    .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                    .Select(g => {
                        var first = g.First();
                        return new {
                            PartyCode = g.Key,
                            PartyName = first.PartyName,
                            SaleValue = g.Sum(x => x.SaleValue),
                            OnBillDiscount = g.Sum(x => x.OnBillDiscount),
                            GrossIncentive = g.Sum(x => x.GrossIncentive),
                            Outstanding = g.Max(x => x.Outstanding),
                            AchievementPercent = g.Max(x => x.AchievementPercent),
                            SlabPercent = g.Max(x => x.SlabPercent)
                        };
                    })
                    .ToList();

                // 6. Generate and save analytics for each active dealer
                foreach (var sale in groupedSales)
                {
                    if (!parties.TryGetValue(sale.PartyCode, out var party)) continue;

                    var isFixedIncentive = party.DealerType == "Fixed Incentive" && !isPreCalculated;
                    var currentIncentive = sale.GrossIncentive;
                    var currentSlabPercent = isFixedIncentive
                        ? (party.FixedIncentivePercent / 100m)
                        : sale.SlabPercent;

                    // A. Slab Progress Tracking & Projection
                    decimal nextSlabPercent = currentSlabPercent;
                    decimal nextSlabTarget = sale.SaleValue;
                    decimal additionalPurchaseRequired = 0m;
                    decimal nextIncentive = currentIncentive;
                    decimal progressPercent = 100m;

                    if (!isFixedIncentive && scheme is not null && scheme.Details.Count > 0)
                    {
                        var currentDetail = scheme.Details.FirstOrDefault(x => sale.SaleValue >= x.MinAchievementPercent && sale.SaleValue <= x.MaxAchievementPercent);
                        var nextDetail = currentDetail is null
                            ? scheme.Details.OrderBy(x => x.MinAchievementPercent).FirstOrDefault(x => x.MinAchievementPercent > sale.SaleValue)
                            : scheme.Details.OrderBy(x => x.MinAchievementPercent).FirstOrDefault(x => x.MinAchievementPercent > currentDetail.MinAchievementPercent);

                        if (nextDetail is not null)
                        {
                            nextSlabPercent = (nextDetail.Percentage ?? 0m) / 100m;
                            nextSlabTarget = nextDetail.MinAchievementPercent;
                            
                            if (nextSlabTarget < sale.SaleValue)
                            {
                                nextSlabTarget = sale.SaleValue;
                            }

                            additionalPurchaseRequired = Math.Round(Math.Max(0m, nextSlabTarget - sale.SaleValue), 0, MidpointRounding.AwayFromZero);
                            progressPercent = nextSlabTarget > 0m ? Math.Round((sale.SaleValue / nextSlabTarget) * 100m, 2, MidpointRounding.AwayFromZero) : 100m;
                            progressPercent = Math.Clamp(progressPercent, -9999.99m, 9999.99m);
                            nextIncentive = Math.Round(Math.Max(0m, (nextDetail.FixedAmount ?? 0m) + nextSlabTarget * ((nextDetail.Percentage ?? 0m) / 100m)), 0, MidpointRounding.AwayFromZero);
                        }
                    }

                    // B. MoM / YoY Growth Analytics with Scaling
                    decimal salesCurrent = sale.SaleValue;
                    decimal salesPriorMonth = 0m;
                    decimal salesPriorYearSamePeriod = 0m;
                    decimal incentiveCurrent = currentIncentive;
                    decimal incentivePriorMonth = 0m;
                    decimal incentivePriorYearSamePeriod = 0m;

                    if (prevIncentives.TryGetValue(sale.PartyCode, out var ps))
                    {
                        salesPriorMonth = ps.SaleValue * scalingFactor;
                        incentivePriorMonth = ps.GrossIncentive * scalingFactor;
                    }

                    if (lastYearIncentives.TryGetValue(sale.PartyCode, out var lys))
                    {
                        salesPriorYearSamePeriod = lys.SaleValue * scalingFactor;
                        incentivePriorYearSamePeriod = lys.GrossIncentive * scalingFactor;
                    }

                    static decimal Growth(decimal current, decimal prior)
                    {
                        if (prior <= 0m)
                        {
                            return current <= 0m ? 0m : 100m;
                        }
                        var val = Math.Round((current - prior) / prior * 100m, 2, MidpointRounding.AwayFromZero);
                        return Math.Clamp(val, -9999.99m, 9999.99m);
                    }

                    decimal salesGrowthMoM = Growth(salesCurrent, salesPriorMonth);
                    decimal salesGrowthYoY = Growth(salesCurrent, salesPriorYearSamePeriod);
                    decimal incentiveGrowthMoM = Growth(incentiveCurrent, incentivePriorMonth);
                    decimal incentiveGrowthYoY = Growth(incentiveCurrent, incentivePriorYearSamePeriod);

                    // C. Determine Smart Trend Label
                    string trend = "Stable";
                    if (salesGrowthMoM < -15m)
                        trend = $"Declining ({salesGrowthMoM:+#0.##%;-#0.##%;0%})";
                    else if (salesGrowthYoY > 30m)
                        trend = $"Fast Growing ({salesGrowthYoY:+#0.##%;-#0.##%;0%})";
                    else if (salesGrowthMoM > 0m)
                        trend = $"Growing ({salesGrowthMoM:+#0.##%;-#0.##%;0%})";

                    // D. Populate and save entities
                    var performance = new DealerMonthlyPerformance
                    {
                        PartyId = party.Id,
                        Month = month,
                        Year = year,
                        TotalSales = salesCurrent,
                        TotalDiscount = sale.OnBillDiscount,
                        SlabPercent = currentSlabPercent,
                        IncentiveEarned = incentiveCurrent,
                        Outstanding = sale.Outstanding,
                        GrowthTrend = trend
                    };
                    db.DealerMonthlyPerformances.Add(performance);

                    var summary = new IncentiveSummary
                    {
                        PartyId = party.Id,
                        Month = month,
                        Year = year,
                        CurrentSale = salesCurrent,
                        CurrentSlabPercent = currentSlabPercent,
                        CurrentIncentive = incentiveCurrent,
                        NextSlabTarget = nextSlabTarget,
                        AdditionalPurchaseRequired = additionalPurchaseRequired,
                        NextIncentive = nextIncentive,
                        ForecastedIncentive = nextIncentive
                    };
                    db.IncentiveSummaries.Add(summary);

                    var progress = new DealerSlabProgress
                    {
                        PartyId = party.Id,
                        Month = month,
                        Year = year,
                        CurrentSale = salesCurrent,
                        NextSlabPercent = nextSlabPercent,
                        NextSlabTarget = nextSlabTarget,
                        ProgressPercent = progressPercent,
                        RemainingAmount = additionalPurchaseRequired
                    };
                    db.DealerSlabProgresses.Add(progress);

                    var growth = new DealerGrowthAnalytics
                    {
                        PartyId = party.Id,
                        Month = month,
                        Year = year,
                        SalesCurrent = salesCurrent,
                        SalesPriorMonth = salesPriorMonth,
                        SalesPriorYearSamePeriod = salesPriorYearSamePeriod,
                        IncentiveCurrent = incentiveCurrent,
                        IncentivePriorMonth = incentivePriorMonth,
                        IncentivePriorYearSamePeriod = incentivePriorYearSamePeriod,
                        SalesGrowthMoM = salesGrowthMoM,
                        SalesGrowthYoY = salesGrowthYoY,
                        IncentiveGrowthMoM = incentiveGrowthMoM,
                        IncentiveGrowthYoY = incentiveGrowthYoY
                    };
                    db.DealerGrowthAnalytics.Add(growth);
                }
            }
            finally
            {
                db.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            await db.SaveChangesAsync(cancellationToken);
            dashboardService.InvalidateCache();

            // Refresh primary branch mapping cache (reads Raw, never writes to it)
            try
            {
                await branchMappingService.RefreshAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Non-critical — log and continue; will refresh on next import or startup
                Console.WriteLine($"[BranchMapping] RefreshAsync failed: {ex.Message}");
            }
        }
        finally
        {
            db.DisableAuditLogs = false;
        }
    }
}
