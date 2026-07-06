using IncentivePortal.DTOs;
using IncentivePortal.Models;
using IncentivePortal.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace IncentivePortal.Application.Reports;

public sealed class Customer360Service(IDbContextFactory<IncentiveDbContext> dbFactory, IMemoryCache memoryCache) : ICustomer360Service
{
    public async Task<Customer360ViewModel> SearchCustomersAsync(string query, CancellationToken cancellationToken = default)
    {
        var vm = new Customer360ViewModel { IsSearchMode = true, SearchQuery = query };

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var q = db.Parties.AsNoTracking().Where(p => !p.IsDeleted);
        
        if (!string.IsNullOrWhiteSpace(query))
        {
            q = q.Where(p => p.PartyCode.Contains(query) || p.PartyName.Contains(query) || p.Mobile.Contains(query) || p.OriginalPartyCode.Contains(query) || p.GST.Contains(query));
        }

        vm.SearchResults = await q.Take(20)
            .Select(p => new DealerSearchDto
            {
                PartyCode = p.PartyCode,
                PartyName = p.PartyName,
                BranchName = p.Branch != null ? p.Branch.Name : "Unknown Branch"
            })
            .ToListAsync(cancellationToken);

        return vm;
    }

    public async Task<Customer360ViewModel> GetCustomer360Async(string? partyCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(partyCode)) return new Customer360ViewModel { IsSearchMode = true };

        await using var dbMain = await dbFactory.CreateDbContextAsync(cancellationToken);
        
        var party = await dbMain.Parties.AsNoTracking()
            .Include(p => p.Branch)
            .FirstOrDefaultAsync(p => !p.IsDeleted && p.PartyCode == partyCode, cancellationToken);

        if (party == null) return new Customer360ViewModel { IsSearchMode = true, SearchQuery = partyCode };

        var vm = new Customer360ViewModel
        {
            PartyCode = party.PartyCode,
            PartyName = party.PartyName,
            GST = party.GST,
            Mobile = party.Mobile,
            Address = party.Address,
            DealerType = party.DealerType,
            BranchName = party.Branch?.Name ?? "Unknown",
            Status = party.Status
        };

        var now = DateTime.UtcNow;
        int currentMonth = now.Month;
        int currentYear = now.Year;
        int fyStartYear = currentMonth >= 4 ? currentYear : currentYear - 1;

        var tasks = new List<Task>();

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var exec = await db.PartyExecutiveMappings.AsNoTracking().FirstOrDefaultAsync(e => !e.IsDeleted && e.PartyCode == partyCode, cancellationToken);
            if (exec != null) vm.ExecutiveName = exec.ExecutiveName;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var bank = await db.BankDetails.AsNoTracking().FirstOrDefaultAsync(b => !b.IsDeleted && b.PartyId == party.Id && b.IsPrimary, cancellationToken);
            if (bank != null)
            {
                vm.BankAccountHolder = bank.AccountHolder;
                vm.BankAccountNumber = bank.AccountNumber;
                vm.BankIFSC = bank.IFSC;
                vm.BankName = bank.BankName;
                vm.BankApprovalStatus = bank.ApprovalStatus;
            }
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var target = await db.DealerTargets.AsNoTracking().FirstOrDefaultAsync(t => !t.IsDeleted && t.PartyCode == partyCode && t.Month == currentMonth && t.Year == currentYear, cancellationToken);
            if (target != null) vm.CurrentMonthTarget = target.FinalTarget;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var monthlyPerf = await db.DealerMonthlyPerformances.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && p.PartyId == party.Id && p.Month == currentMonth && p.Year == currentYear, cancellationToken);
            if (monthlyPerf != null) {
                vm.CurrentMonthSales = monthlyPerf.TotalSales;
            } else {
                vm.CurrentMonthSales = await db.Raws.AsNoTracking().Where(r => !r.IsDeleted && r.MonthNumber == currentMonth && r.YearNumber == currentYear && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode)).SumAsync(r => (decimal?)r.NetRetailSelling, cancellationToken) ?? 0m;
            }
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var outstanding = await db.DealerOutstandings.AsNoTracking().OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(o => !o.IsDeleted && o.PartyCode == partyCode, cancellationToken);
            if (outstanding != null) vm.CurrentOutstanding = outstanding.Outstanding;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            vm.YTDSales = await db.Raws.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode) &&
                            ((r.YearNumber == fyStartYear && r.MonthNumber >= 4) || (r.YearNumber == fyStartYear + 1 && r.MonthNumber < 4)) &&
                            (r.YearNumber < currentYear || (r.YearNumber == currentYear && r.MonthNumber <= currentMonth)))
                .SumAsync(r => (decimal?)r.NetRetailSelling, cancellationToken) ?? 0m;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var slabProgress = await db.DealerSlabProgresses.AsNoTracking().FirstOrDefaultAsync(p => !p.IsDeleted && p.PartyId == party.Id && p.Month == currentMonth && p.Year == currentYear, cancellationToken);
            if (slabProgress != null)
            {
                vm.NextSlabPercent = slabProgress.NextSlabPercent;
                vm.NextSlabTarget = slabProgress.NextSlabTarget;
                vm.ProgressToNextSlabPercent = slabProgress.ProgressPercent;
            }
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var incSum = await db.IncentiveSummaries.AsNoTracking().FirstOrDefaultAsync(s => !s.IsDeleted && s.PartyId == party.Id && s.Month == currentMonth && s.Year == currentYear, cancellationToken);
            if (incSum != null) vm.CurrentSlabPercent = incSum.CurrentSlabPercent;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var last6MonthsPeriods = new List<(int Month, int Year)>();
            int curM = currentMonth, curY = currentYear;
            for (int i = 0; i < 6; i++) {
                last6MonthsPeriods.Add((curM, curY));
                curM--; if (curM == 0) { curM = 12; curY--; }
            }
            last6MonthsPeriods.Reverse();

            var minYear = last6MonthsPeriods.Min(x => x.Year);
            
            var trendRaw = await db.Raws.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode) && r.YearNumber >= minYear)
                .GroupBy(r => new { r.YearNumber, r.MonthNumber })
                .Select(g => new { Year = g.Key.YearNumber, Month = g.Key.MonthNumber, Sales = g.Sum(x => (decimal?)x.NetRetailSelling) ?? 0m })
                .ToListAsync(cancellationToken);

            var trendPerf = await db.DealerMonthlyPerformances.AsNoTracking()
                .Where(dp => !dp.IsDeleted && dp.PartyId == party.Id && dp.Year >= minYear)
                .ToListAsync(cancellationToken);

            lock(vm.TrendLabels)
            {
                foreach (var period in last6MonthsPeriods)
                {
                    string label = new DateTime(period.Year, period.Month, 1).ToString("MMM yy");
                    vm.TrendLabels.Add(label);

                    var p = trendPerf.FirstOrDefault(x => x.Year == period.Year && x.Month == period.Month);
                    if (p != null)
                    {
                        vm.SalesTrend.Add(p.TotalSales);
                        vm.IncentiveTrend.Add(p.IncentiveEarned);
                    }
                    else
                    {
                        var rawSales = trendRaw.FirstOrDefault(x => x.Year == period.Year && x.Month == period.Month)?.Sales ?? 0m;
                        vm.SalesTrend.Add(rawSales);
                        vm.IncentiveTrend.Add(0m);
                    }
                }
            }
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var baseRawQuery = db.Raws.AsNoTracking().Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode));
            
            var rawMonthlyData = await baseRawQuery
                .Where(r => r.YearNumber.HasValue && r.MonthNumber.HasValue)
                .GroupBy(r => new { Year = r.YearNumber!.Value, MonthNum = r.MonthNumber!.Value, r.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    MonthNum = g.Key.MonthNum,
                    MonthName = g.Key.Month ?? "",
                    Sales = g.Sum(x => (decimal?)x.NetRetailSelling) ?? 0m
                })
                .ToListAsync(cancellationToken);

            lock (vm.AvailableYears)
            {
                vm.AvailableYears.AddRange(rawMonthlyData.Select(x => x.Year).Distinct().OrderBy(x => x).ToList());
                var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                for (int m = 1; m <= 12; m++)
                {
                    var row = new YearMonthSalesDto { MonthNumber = m, MonthName = monthNames[m - 1] };
                    foreach (var year in vm.AvailableYears)
                    {
                        var currentSales = rawMonthlyData.FirstOrDefault(x => x.Year == year && x.MonthNum == m)?.Sales ?? 0m;
                        var prevSales = rawMonthlyData.FirstOrDefault(x => x.Year == year - 1 && x.MonthNum == m)?.Sales;
                        decimal? growth = null;
                        if (prevSales.HasValue && prevSales.Value > 0)
                            growth = ((currentSales - prevSales.Value) / prevSales.Value) * 100m;
                        row.YearlyData[year] = new YearlySalesData { Sales = currentSales, GrowthPercentage = growth };
                    }
                    vm.MonthYearMatrix.Add(row);
                }
            }
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var catSales = await db.Raws.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode))
                .GroupBy(r => r.PartCategoryCode)
                .Select(g => new SalesSummaryDto
                {
                    Label = g.Key ?? "Unknown",
                    TotalSales = g.Sum(x => (decimal?)x.NetRetailSelling) ?? 0m,
                    TotalQty = g.Sum(x => (decimal?)x.NetRetailQty) ?? 0m
                })
                .OrderByDescending(x => x.TotalSales)
                .Take(12)
                .ToListAsync(cancellationToken);
            lock(vm.CategoryWiseSales) vm.CategoryWiseSales = catSales;
        }));

        tasks.Add(Task.Run(async () => {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var recTrans = await db.Raws.AsNoTracking()
                .Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode))
                .OrderByDescending(r => r.YearNumber).ThenByDescending(r => r.MonthNumber)
                .Take(10)
                .Select(r => new RecentTransactionDto
                {
                    Date = r.MonthYear ?? (r.MonthNumber + "-" + r.YearNumber),
                    DocumentNum = r.DocumentNum ?? "N/A",
                    Category = r.PartCategoryCode ?? "Unknown",
                    NetSales = r.NetRetailSelling
                })
                .ToListAsync(cancellationToken);
            lock(vm.RecentTransactions) vm.RecentTransactions = recTrans;
        }));

        await Task.WhenAll(tasks);

        if (vm.ProgressToNextSlabPercent == 0 && vm.CurrentMonthTarget > 0)
        {
            vm.ProgressToNextSlabPercent = (vm.CurrentMonthSales / vm.CurrentMonthTarget) * 100m;
        }

        return vm;
    }
    
    public async Task<DataTableResponse<SalesAnalysisDto>> GetSalesAnalysisAsync(string partyCode, int start, int length, string search, string sortCol, string sortDir, CancellationToken cancellationToken = default)
    {
        var response = new DataTableResponse<SalesAnalysisDto>();
        if (string.IsNullOrWhiteSpace(partyCode)) return response;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.Raws.AsNoTracking()
            .Where(r => !r.IsDeleted && (r.OriginalCode == partyCode || r.ConsPartyCode == partyCode));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => r.PartNum.Contains(search) || r.PartCategoryCode.Contains(search) || r.MonthYear.Contains(search));
        }

        response.recordsTotal = await query.Select(r => new { r.MonthYear, r.YearNumber, r.MonthNumber, r.PartCategoryCode, r.PartNum }).Distinct().CountAsync(cancellationToken);
        response.recordsFiltered = response.recordsTotal;

        var groupedQuery = query.GroupBy(r => new { r.MonthYear, r.YearNumber, r.MonthNumber, r.PartCategoryCode, r.PartNum })
            .Select(g => new SalesAnalysisDto
            {
                MonthYear = g.Key.MonthYear,
                Category = g.Key.PartCategoryCode,
                PartNum = g.Key.PartNum,
                TotalSales = g.Sum(x => (decimal?)x.NetRetailSelling) ?? 0m,
                TotalQty = g.Sum(x => (decimal?)x.NetRetailQty) ?? 0m
            });

        bool isAsc = sortDir?.ToLower() == "asc";
        groupedQuery = sortCol switch
        {
            "MonthYear" => isAsc ? groupedQuery.OrderBy(x => x.MonthYear) : groupedQuery.OrderByDescending(x => x.MonthYear),
            "Category" => isAsc ? groupedQuery.OrderBy(x => x.Category) : groupedQuery.OrderByDescending(x => x.Category),
            "PartNum" => isAsc ? groupedQuery.OrderBy(x => x.PartNum) : groupedQuery.OrderByDescending(x => x.PartNum),
            "TotalSales" => isAsc ? groupedQuery.OrderBy(x => x.TotalSales) : groupedQuery.OrderByDescending(x => x.TotalSales),
            "TotalQty" => isAsc ? groupedQuery.OrderBy(x => x.TotalQty) : groupedQuery.OrderByDescending(x => x.TotalQty),
            _ => groupedQuery.OrderByDescending(x => x.TotalSales)
        };

        response.data = await groupedQuery.Skip(start).Take(length > 0 ? length : 10).ToListAsync(cancellationToken);
        return response;
    }
}
