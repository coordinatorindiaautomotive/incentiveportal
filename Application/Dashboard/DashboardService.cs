using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Helpers;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface for generating high-performance dashboard analytical metrics.
/// </summary>
public interface IDashboardService
{
    /// <summary>
    /// Compiles a fully unified Dashboard summary report including trends, insights, and work queue checklists.
    /// Uses absolute caching in memory to ensure sub-millisecond response rates under concurrent load.
    /// </summary>
    Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the current dashboard memory cache globally. Call this whenever fresh uploads occur.
    /// </summary>
    void InvalidateCache();
}

/// <summary>
/// Sealed implementation of <see cref="IDashboardService"/> containing EF Core metrics compilations and 5-minute memory caching.
/// </summary>
public sealed class DashboardService(IncentiveDbContext db, ICurrentUser currentUser, IMemoryCache cache) : IDashboardService
{
    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var version = cache.GetOrCreate("DashboardVersion", entry => 1);
        var cacheKey = $"DashboardSummary_V{version}_{currentUser.UserName}";

        return (await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            var query = db.SsIncentives.AsNoTracking()
                .Include(x => x.ImportLog)
                .Where(x => x.ImportLogId > 0 && !x.ImportLog!.IsHistorical)
                .AsQueryable();

            if (currentUser.IsInRole(AppRoles.SalesExecutive))
            {
                var mappedPartyCodes = await db.PartyExecutiveMappings
                    .AsNoTracking()
                    .Where(x => x.ExecutiveCode == currentUser.UserName)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);

                query = query.Where(x => mappedPartyCodes.Contains(x.PartyCode));
            }
            else if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                var branchPartyCodes = await db.Parties
                    .AsNoTracking()
                    .Where(x => x.BranchId == currentUser.BranchId.Value)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);

                query = query.Where(x => branchPartyCodes.Contains(x.PartyCode));
            }

            var latestPeriod = await query
                .Select(x => new { x.Year, x.Month })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .FirstOrDefaultAsync(cancellationToken);

            var currentYear = latestPeriod?.Year ?? DateTime.UtcNow.Year;
            var currentMonth = latestPeriod?.Month ?? DateTime.UtcNow.Month;
            var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
            var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;
            var lastYearMonth = currentMonth;
            var lastYearYear = currentYear - 1;

            var currentSalesRaw = await query.Where(x => x.Year == currentYear && x.Month == currentMonth).ToListAsync(cancellationToken);
            var currentSales = currentSalesRaw
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var first = g.First();
                    return new
                    {
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

            var previousSalesRaw = await query.Where(x => x.Year == previousYear && x.Month == previousMonth).ToListAsync(cancellationToken);
            var previousSales = previousSalesRaw
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var first = g.First();
                    return new
                    {
                        PartyCode = g.Key,
                        PartyName = first.PartyName,
                        SaleValue = g.Sum(x => x.SaleValue),
                        GrossIncentive = g.Sum(x => x.GrossIncentive)
                    };
                })
                .ToList();

            var lastYearSalesRaw = await query.Where(x => x.Year == lastYearYear && x.Month == lastYearMonth).ToListAsync(cancellationToken);
            var lastYearSales = lastYearSalesRaw
                .GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => {
                    var first = g.First();
                    return new
                    {
                        PartyCode = g.Key,
                        PartyName = first.PartyName,
                        SaleValue = g.Sum(x => x.SaleValue),
                        GrossIncentive = g.Sum(x => x.GrossIncentive)
                    };
                })
                .ToList();

            decimal selectedTotalSales() => currentSales.Sum(x => x.SaleValue);
            decimal selectedTotalDiscount() => currentSales.Sum(x => x.OnBillDiscount);
            decimal selectedTotalIncentive() => currentSales.Sum(x => x.GrossIncentive);
            decimal previousSalesTotal() => previousSales.Sum(x => x.SaleValue);
            decimal previousIncentiveTotal() => previousSales.Sum(x => x.GrossIncentive);
            decimal lastYearSalesTotal() => lastYearSales.Sum(x => x.SaleValue);
            decimal lastYearIncentiveTotal() => lastYearSales.Sum(x => x.GrossIncentive);

            static decimal Growth(decimal current, decimal previous)
                => previous <= 0 ? (current <= 0 ? 0 : 100) : Math.Round((current - previous) / previous * 100, 2);

            var branchPerformanceRaw = await (from s in query.Where(x => x.Year == currentYear && x.Month == currentMonth)
                                               join b in db.Branches on s.SourceLocation equals b.Code into branchJoin
                                               from b in branchJoin.DefaultIfEmpty()
                                               group s by new { BranchName = b != null ? b.Name : s.SourceLocation, BranchCode = s.SourceLocation } into g
                                               select new
                                               {
                                                   BranchName = g.Key.BranchName,
                                                   BranchCode = g.Key.BranchCode,
                                                   Sales = g.Sum(x => x.SaleValue),
                                                   Incentive = g.Sum(x => x.GrossIncentive)
                                               })
                .OrderByDescending(x => x.Sales)
                .Take(8)
                .ToListAsync(cancellationToken);

            var branchPerformance = branchPerformanceRaw
                .Select(x => new BranchPerformanceDto(x.BranchName, x.Sales, x.Incentive))
                .ToList();

            var prevSalesByParty = previousSales.ToDictionary(g => g.PartyCode, g => g.SaleValue, StringComparer.OrdinalIgnoreCase);
            var lastYearSalesByParty = lastYearSales.ToDictionary(g => g.PartyCode, g => g.SaleValue, StringComparer.OrdinalIgnoreCase);

            var topDealers = currentSales
                .Select(x =>
                {
                    var salesValue = x.SaleValue;
                    var incentive = x.GrossIncentive;
                    var previousSalesValue = prevSalesByParty.GetValueOrDefault(x.PartyCode);
                    var lastYearSalesValue = lastYearSalesByParty.GetValueOrDefault(x.PartyCode);
                    var growthMoM = Growth(salesValue, previousSalesValue);
                    var growthYoY = Growth(salesValue, lastYearSalesValue);
                    var slabPercent = x.SlabPercent * 100m;
                    return new PartyPerformanceDto(x.PartyCode, x.PartyName, salesValue, incentive, slabPercent, growthMoM, growthYoY, 0);
                })
                .OrderByDescending(x => x.Sales)
                .Take(5)
                .ToList();

            var weakDealers = currentSales
                .Select(x =>
                {
                    var salesValue = x.SaleValue;
                    var incentive = x.GrossIncentive;
                    var previousSalesValue = prevSalesByParty.GetValueOrDefault(x.PartyCode);
                    var lastYearSalesValue = lastYearSalesByParty.GetValueOrDefault(x.PartyCode);
                    var growthMoM = Growth(salesValue, previousSalesValue);
                    var growthYoY = Growth(salesValue, lastYearSalesValue);
                    var slabPercent = x.SlabPercent * 100m;
                    return new PartyPerformanceDto(x.PartyCode, x.PartyName, salesValue, incentive, slabPercent, growthMoM, growthYoY, 0);
                })
                .OrderBy(x => x.GrowthMoM)
                .Take(5)
                .ToList();

            var lastImport = await db.ImportLogs.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            var pendingApprovals = await db.BankApprovalRequests.CountAsync(x => x.Status == "Pending", cancellationToken);
            var pendingTransfers = await db.SsIncentives.CountAsync(x => (x.PaymentStatus == "Pending" || x.PaymentStatus == "Credit Party") && !x.IsDeleted, cancellationToken);
            var unreconciledAmount = await db.SsIncentives.Where(x => (x.PaymentStatus == "Pending" || x.PaymentStatus == "Credit Party") && !x.IsDeleted).SumAsync(x => (decimal?)x.NetTransferAmount, cancellationToken) ?? 0;
            var workQueue = new List<DashboardWorkItem>();

            if (!currentUser.IsInRole(AppRoles.SalesExecutive))
            {
                if (pendingApprovals > 0)
                    workQueue.Add(new DashboardWorkItem("Bank approvals pending", $"{pendingApprovals:N0} request(s) need HO Finance review.", "High", "/Reports/PendingApprovals"));
                if (pendingTransfers > 0)
                    workQueue.Add(new DashboardWorkItem("Transfers awaiting UTR", $"{pendingTransfers:N0} transfer(s), {unreconciledAmount:N0} pending reconciliation.", "Medium", "/Transfers"));
                if (lastImport is null)
                    workQueue.Add(new DashboardWorkItem("No sales import found", "Import the monthly incentive workbook to start calculations.", "High", "/Imports/MonthlySales"));
                if (!await db.BankDetails.AnyAsync(x => x.ApprovalStatus == "Approved", cancellationToken))
                    workQueue.Add(new DashboardWorkItem("Approved bank master is empty", "Approved bank details are required before final transfer export.", "Medium", "/Reports/IncentiveRegister"));
            }

            var performanceComparisons = new List<PeriodComparison>
            {
                new PeriodComparison("Sales", selectedTotalSales(), previousSalesTotal(), Growth(selectedTotalSales(), previousSalesTotal()), $"{new DateTime(currentYear, currentMonth, 1):MMM yyyy} vs {new DateTime(previousYear, previousMonth, 1):MMM yyyy}"),
                new PeriodComparison("Incentive", selectedTotalIncentive(), previousIncentiveTotal(), Growth(selectedTotalIncentive(), previousIncentiveTotal()), $"{new DateTime(currentYear, currentMonth, 1):MMM yyyy} vs {new DateTime(previousYear, previousMonth, 1):MMM yyyy}"),
                new PeriodComparison("Sales YoY", selectedTotalSales(), lastYearSalesTotal(), Growth(selectedTotalSales(), lastYearSalesTotal()), $"{new DateTime(currentYear, currentMonth, 1):MMM yyyy} vs {new DateTime(lastYearYear, lastYearMonth, 1):MMM yyyy}"),
                new PeriodComparison("Incentive YoY", selectedTotalIncentive(), lastYearIncentiveTotal(), Growth(selectedTotalIncentive(), lastYearIncentiveTotal()), $"{new DateTime(currentYear, currentMonth, 1):MMM yyyy} vs {new DateTime(lastYearYear, lastYearMonth, 1):MMM yyyy}")
            };

            var smartInsights = new List<SmartInsightDto>();
            foreach (var dealer in weakDealers.Where(x => x.GrowthMoM < -15m))
            {
                smartInsights.Add(new SmartInsightDto("Decline", $"Declining: {dealer.PartyName} sales down {Math.Abs(dealer.GrowthMoM):0.##}% compared to last month.", "High"));
            }
            foreach (var dealer in topDealers.Where(x => x.GrowthYoY > 30m))
            {
                smartInsights.Add(new SmartInsightDto("Growth", $"Fast Growing: {dealer.PartyName} sales grew {dealer.GrowthYoY:0.##}% YoY.", "Low"));
            }
            var closeDealers = currentSales.Where(x => x.AchievementPercent >= 80m && x.AchievementPercent < 100m).Take(3).ToList();
            foreach (var sale in closeDealers)
            {
                decimal targetSale = sale.AchievementPercent > 0 ? sale.SaleValue * 100m / sale.AchievementPercent : sale.SaleValue;
                decimal remaining = Math.Max(0m, targetSale - sale.SaleValue);
                if (remaining > 0 && remaining <= 25000m)
                {
                    smartInsights.Add(new SmartInsightDto("Target", $"Close to Slab: Only {remaining:C0} more purchase required for {sale.PartyName} to unlock next slab.", "Medium"));
                }
            }
            if (smartInsights.Count == 0)
            {
                smartInsights.Add(new SmartInsightDto("Stable", "All active dealers are performing at stable growth rates this month.", "Low"));
            }

            var trendQuery = query;

            var last6MonthsRaw = await trendQuery
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Sales = g.Sum(x => x.SaleValue),
                    Incentive = g.Sum(x => x.GrossIncentive)
                })
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .Take(6)
                .ToListAsync(cancellationToken);
            last6MonthsRaw.Reverse();
            var monthsLabels = last6MonthsRaw.Select(x => $"{new DateTime(x.Year, x.Month, 1):MMM-yy}").ToList();
            var salesTrend = last6MonthsRaw.Select(x => x.Sales).ToList();
            var incentiveTrend = last6MonthsRaw.Select(x => x.Incentive).ToList();

            var slabDistribution = currentSales
                .GroupBy(x => x.SlabPercent)
                .Select(g => new
                {
                    Slab = $"{g.Key * 100:0}% Slab",
                    Count = g.Count()
                })
                .OrderBy(x => x.Slab)
                .ToList();
            var slabRanges = slabDistribution.Select(x => x.Slab).ToList();
            var slabCounts = slabDistribution.Select(x => x.Count).ToList();

            // --- Location Split (sales by branch/location, top 8) ---
            var locationSplit = branchPerformanceRaw
                .OrderByDescending(x => x.Sales)
                .Take(8)
                .ToList();
            var locationLabels = locationSplit.Select(x => x.BranchName ?? x.BranchCode).ToList();
            var locationSalesVals = locationSplit.Select(x => Math.Round(x.Sales, 0)).ToList();

            // --- Category Mix (sales by PartCategoryCode from current raw records) ---
            var categoryMixRaw = await db.Raws
                .AsNoTracking()
                .Where(x => x.YearNumber == currentYear && x.MonthNumber == currentMonth && !x.IsDeleted)
                .GroupBy(x => x.PartCategoryCode ?? "Other")
                .Select(g => new { Category = g.Key, Sales = g.Sum(x => x.NetRetailSelling) })
                .OrderByDescending(x => x.Sales)
                .Take(8)
                .ToListAsync(cancellationToken);
            var categoryLabels = categoryMixRaw.Select(x => string.IsNullOrWhiteSpace(x.Category) ? "Other" : x.Category).ToList();
            var categorySalesVals = categoryMixRaw.Select(x => Math.Round(x.Sales, 0)).ToList();

            var chartData = new DashboardChartDataDto(
                salesTrend,
                incentiveTrend,
                monthsLabels,
                branchPerformance.Select(x => x.BranchName).ToList(),
                branchPerformance.Select(x => x.Sales).ToList(),
                slabRanges,
                slabCounts,
                locationLabels,
                locationSalesVals,
                categoryLabels,
                categorySalesVals);

            var prevBranchSalesRaw = await (from s in query.Where(x => x.Year == previousYear && x.Month == previousMonth)
                                            join b in db.Branches on s.SourceLocation equals b.Code into branchJoin
                                            from b in branchJoin.DefaultIfEmpty()
                                            group s by (b != null ? b.Name : s.SourceLocation) into g
                                            select new { BranchName = g.Key, Sales = g.Sum(x => x.SaleValue) })
                                           .ToListAsync(cancellationToken);
            var prevBranchSales = prevBranchSalesRaw.ToDictionary(x => x.BranchName, x => x.Sales, StringComparer.OrdinalIgnoreCase);

            var branchRankings = branchPerformanceRaw.Select(x => new BranchRankingDto(
                x.BranchName,
                x.BranchCode,
                x.Sales,
                x.Incentive,
                Growth(x.Sales, prevBranchSales.GetValueOrDefault(x.BranchName))
            )).OrderByDescending(x => x.TotalSales).ToList();

            decimal budget = 1500000m;
            decimal budgetVsActual = Math.Round((selectedTotalIncentive() / budget) * 100m, 2);
            decimal avgIncentivePercent = selectedTotalSales() > 0 ? Math.Round((selectedTotalIncentive() / selectedTotalSales()) * 100m, 2) : 0m;

            var histRows = await db.ImportLogs.Where(x => x.IsDeleted == false && x.IsHistorical).SumAsync(x => (int?)x.SuccessRows, cancellationToken) ?? 0;
            var currYearRows = await db.ImportLogs.Where(x => x.IsDeleted == false && !x.IsHistorical && x.Year == DateTime.UtcNow.Year).SumAsync(x => (int?)x.SuccessRows, cancellationToken) ?? 0;
            var monthLock = await db.MonthLocks.AnyAsync(x => x.LockYear == DateTime.UtcNow.Year && x.LockMonth == DateTime.UtcNow.Month && x.IsLocked, cancellationToken);
            
            var ledgerQueryForSums = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted).AsQueryable();
            if (currentUser.IsInRole(AppRoles.SalesExecutive))
            {
                var mappedPartyCodes = await db.PartyExecutiveMappings
                    .AsNoTracking()
                    .Where(x => x.ExecutiveCode == currentUser.UserName)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);
                ledgerQueryForSums = ledgerQueryForSums.Where(x => mappedPartyCodes.Contains(x.PartyCode));
            }
            else if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                var branchParties = await db.Parties
                    .AsNoTracking()
                    .Where(x => x.BranchId == currentUser.BranchId.Value)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);
                ledgerQueryForSums = ledgerQueryForSums.Where(x => branchParties.Contains(x.PartyCode));
            }

            var paidInc = await ledgerQueryForSums.Where(x => x.PaymentStatus == "Paid").SumAsync(x => (decimal?)x.NetTransferAmount, cancellationToken) ?? 0;
            var pendInc = await ledgerQueryForSums.Where(x => x.PaymentStatus == "Pending" || x.PaymentStatus == "Credit Party").SumAsync(x => (decimal?)x.NetTransferAmount, cancellationToken) ?? 0;

            return new DashboardSummary(
                selectedTotalIncentive(),
                pendingApprovals,
                unreconciledAmount,
                await db.Parties.CountAsync(cancellationToken),
                selectedTotalSales(),
                selectedTotalDiscount(),
                currentSales.Count,
                pendingTransfers,
                await db.SsIncentives.CountAsync(x => x.PaymentStatus == "Paid" && !x.IsDeleted, cancellationToken),
                lastImport?.FileName ?? "No import yet",
                lastImport?.CreatedAt,
                Growth(selectedTotalSales(), previousSalesTotal()),
                Growth(selectedTotalIncentive(), previousIncentiveTotal()),
                Growth(selectedTotalSales(), lastYearSalesTotal()),
                Growth(selectedTotalIncentive(), lastYearIncentiveTotal()),
                performanceComparisons,
                topDealers,
                weakDealers,
                branchPerformance,
                workQueue,
                branchRankings,
                smartInsights,
                chartData,
                budgetVsActual,
                avgIncentivePercent,
                histRows,
                currYearRows,
                monthLock ? "Locked 🔒" : "Pending ⏳",
                paidInc,
                pendInc);
        }))!;
    }

    public void InvalidateCache()
    {
        var current = cache.GetOrCreate("DashboardVersion", entry => 1);
        cache.Set("DashboardVersion", current + 1);
    }
}
