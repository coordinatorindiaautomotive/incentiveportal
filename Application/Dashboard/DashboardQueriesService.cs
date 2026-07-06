using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Helpers;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IDashboardQueriesService
{
    Task<object> GetKPIsAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetTrendAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        string? view,
        CancellationToken cancellationToken);

    Task<object> GetCategoryMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetPartyMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetQuarterSummaryAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetLocationRankingAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetComparisonAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        string? cmp,
        CancellationToken cancellationToken);

    Task<object> GetConsigneeSummaryAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetConsigneeMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);

    Task<object> GetDealerSubTypeMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken);
}

public sealed class DashboardQueriesService(IncentiveDbContext db, ICurrentUser currentUser) : IDashboardQueriesService
{
    private static readonly string[] MO_ORDER = ["Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar"];

    private struct FilterContext
    {
        public int AnchorYear;
        public int TargetYear;
        public int CompYear;
        public bool IsCurrentYear;
        public string Metric;
        public List<string> Quarters;
        public List<int> MonthNums;
        public List<string> PartyTypes;
        public List<string> Categories;
        public List<string> Consignees;
        public List<string> DealerSubTypes;
        public List<string> Locations;
    }

    private async Task<FilterContext> ParseFiltersAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        int anchorYear = DateTime.UtcNow.Year;
        var latestLedger = await db.SsIncentives
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.ImportLogId > 0)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestLedger != null)
        {
            anchorYear = latestLedger.Month >= 4 ? latestLedger.Year : latestLedger.Year - 1;
        }

        int targetYear = anchorYear;
        bool isCurrentYear = true;

        if (yr != null && yr.Equals("LY", StringComparison.OrdinalIgnoreCase))
        {
            targetYear = anchorYear - 1;
            isCurrentYear = false;
        }

        int compYear = targetYear - 1;

        var quarterList = string.IsNullOrWhiteSpace(quarters)
            ? []
            : quarters.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(q => q.Trim().ToUpperInvariant()).ToList();

        var monthNumsList = new List<int>();
        if (!string.IsNullOrWhiteSpace(months))
        {
            foreach (var m in months.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var clean = m.Trim();
                if (int.TryParse(clean, out int parsed))
                {
                    monthNumsList.Add(parsed);
                }
                else
                {
                    var mNum = clean.ToLower() switch
                    {
                        "jan" => 1,
                        "feb" => 2,
                        "mar" => 3,
                        "apr" => 4,
                        "may" => 5,
                        "jun" => 6,
                        "jul" => 7,
                        "aug" => 8,
                        "sep" => 9,
                        "oct" => 10,
                        "nov" => 11,
                        "dec" => 12,
                        _ => 0
                    };
                    if (mNum > 0)
                        monthNumsList.Add(mNum);
                }
            }
        }

        var partyTypesList = string.IsNullOrWhiteSpace(partyTypes)
            ? []
            : partyTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToUpperInvariant()).ToList();

        var categoriesList = string.IsNullOrWhiteSpace(categories)
            ? []
            : categories.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToUpperInvariant()).ToList();

        var consigneesList = string.IsNullOrWhiteSpace(consignees)
            ? []
            : consignees.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToUpperInvariant()).ToList();

        var dealerSubTypesList = string.IsNullOrWhiteSpace(dealerSubTypes)
            ? []
            : dealerSubTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim().ToUpperInvariant()).ToList();

        var locationsList = string.IsNullOrWhiteSpace(locations)
            ? []
            : locations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().ToUpperInvariant()).ToList();

        return new FilterContext
        {
            AnchorYear = anchorYear,
            TargetYear = targetYear,
            CompYear = compYear,
            IsCurrentYear = isCurrentYear,
            Metric = met ?? "Net_Sales",
            Quarters = quarterList,
            MonthNums = monthNumsList,
            PartyTypes = partyTypesList,
            Categories = categoriesList,
            Consignees = consigneesList,
            DealerSubTypes = dealerSubTypesList,
            Locations = locationsList
        };
    }

    private async Task<IQueryable<RawRecord>> GetFilteredQueryAsync(FilterContext fc, bool compareYear, CancellationToken cancellationToken)
    {
        var yr = compareYear ? fc.CompYear : fc.TargetYear;

        var query = db.Raws.AsNoTracking().Where(x => !x.IsDeleted && x.ImportLogId > 0 &&
            ((x.YearNumber == yr && x.MonthNumber >= 4) || (x.YearNumber == yr + 1 && x.MonthNumber <= 3)));

        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var mappedPartyCodes = await db.PartyExecutiveMappings
                .AsNoTracking()
                .Where(x => x.ExecutiveCode == currentUser.UserName)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => mappedPartyCodes.Contains(x.OriginalCode ?? x.ConsPartyCode ?? ""));
        }
        else if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var branchParties = await db.Parties
                .AsNoTracking()
                .Where(x => x.BranchId == currentUser.BranchId.Value)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => branchParties.Contains(x.OriginalCode ?? x.ConsPartyCode ?? ""));
        }

        if (fc.Quarters.Count > 0)
            query = query.Where(x => x.Quarter != null && fc.Quarters.Contains(x.Quarter));

        if (fc.MonthNums.Count > 0)
            query = query.Where(x => x.MonthNumber.HasValue && fc.MonthNums.Contains(x.MonthNumber.Value));

        if (fc.PartyTypes.Count > 0)
        {
            var pTypes = fc.PartyTypes.ToList();
            query = from x in query
                    join p in db.Parties on (x.OriginalCode ?? x.ConsPartyCode) equals p.PartyCode into pj
                    from p in pj.DefaultIfEmpty()
                    where (p != null && p.DealerType != null && pTypes.Contains(p.DealerType.ToUpper())) ||
                          (p == null && x.PartyType != null && pTypes.Contains(x.PartyType.ToUpper()))
                    select x;
        }

        if (fc.Categories.Count > 0)
            query = query.Where(x => x.PartCategoryCode != null && fc.Categories.Contains(x.PartCategoryCode.ToUpper()));

        if (fc.Consignees.Count > 0)
        {
            var branchCodes = await db.Branches
                .AsNoTracking()
                .Where(b => b.Consignee != null && fc.Consignees.Contains(b.Consignee))
                .Select(b => b.Code)
                .ToListAsync(cancellationToken);
            query = query.Where(x => x.Loc != null && branchCodes.Contains(x.Loc));
        }

        if (fc.DealerSubTypes.Count > 0)
            query = query.Where(x => x.DealerSubType != null && fc.DealerSubTypes.Contains(x.DealerSubType.ToUpper()));

        if (fc.Locations.Count > 0)
            query = query.Where(x => x.Loc != null && fc.Locations.Contains(x.Loc.ToUpper()));

        return query;
    }

    private async Task<IQueryable<RawRecord>> GetFilteredQueryWithMonthsAsync(
        FilterContext fc, 
        bool compareYear, 
        List<int> ytdMonths, 
        CancellationToken cancellationToken,
        int? latestMonth = null,
        int? maxDay = null)
    {
        var yr = compareYear ? fc.CompYear : fc.TargetYear;

        var query = db.Raws.AsNoTracking().Where(x => !x.IsDeleted && x.ImportLogId > 0 &&
            ((x.YearNumber == yr && x.MonthNumber >= 4) || (x.YearNumber == yr + 1 && x.MonthNumber <= 3)));

        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var mappedPartyCodes = await db.PartyExecutiveMappings
                .AsNoTracking()
                .Where(x => x.ExecutiveCode == currentUser.UserName)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => mappedPartyCodes.Contains(x.OriginalCode ?? x.ConsPartyCode ?? ""));
        }
        else if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var branchParties = await db.Parties
                .AsNoTracking()
                .Where(x => x.BranchId == currentUser.BranchId.Value)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => branchParties.Contains(x.OriginalCode ?? x.ConsPartyCode ?? ""));
        }

        if (fc.Quarters.Count > 0)
            query = query.Where(x => x.Quarter != null && fc.Quarters.Contains(x.Quarter));

        if (ytdMonths.Count > 0)
            query = query.Where(x => x.MonthNumber.HasValue && ytdMonths.Contains(x.MonthNumber.Value));

        if (fc.PartyTypes.Count > 0)
        {
            var pTypes = fc.PartyTypes.ToList();
            query = from x in query
                    join p in db.Parties on (x.OriginalCode ?? x.ConsPartyCode) equals p.PartyCode into pj
                    from p in pj.DefaultIfEmpty()
                    where (p != null && p.DealerType != null && pTypes.Contains(p.DealerType.ToUpper())) ||
                          (p == null && x.PartyType != null && pTypes.Contains(x.PartyType.ToUpper()))
                    select x;
        }

        if (fc.Categories.Count > 0)
            query = query.Where(x => x.PartCategoryCode != null && fc.Categories.Contains(x.PartCategoryCode.ToUpper()));

        if (fc.Consignees.Count > 0)
        {
            var branchCodes = await db.Branches
                .AsNoTracking()
                .Where(b => b.Consignee != null && fc.Consignees.Contains(b.Consignee))
                .Select(b => b.Code)
                .ToListAsync(cancellationToken);
            query = query.Where(x => x.Loc != null && branchCodes.Contains(x.Loc));
        }

        if (fc.DealerSubTypes.Count > 0)
            query = query.Where(x => x.DealerSubType != null && fc.DealerSubTypes.Contains(x.DealerSubType.ToUpper()));

        if (fc.Locations.Count > 0)
            query = query.Where(x => x.Loc != null && fc.Locations.Contains(x.Loc.ToUpper()));

        if (latestMonth.HasValue && maxDay.HasValue)
        {
            query = query.Where(x => x.MonthNumber != latestMonth.Value || !x.Day.HasValue || x.Day.Value <= maxDay.Value);
        }

        return query;
    }

    private static async Task<decimal> ApplyMetricSumAsync(IQueryable<RawRecord> query, string metric, CancellationToken cancellationToken)
    {
        return metric switch
        {
            "Net_Sales" => await query.SumAsync(x => x.NetRetailSelling, cancellationToken),
            "Net_DDL" => await query.SumAsync(x => x.NetRetailDdl, cancellationToken),
            "Discount" => await query.SumAsync(x => x.DiscountAmount, cancellationToken),
            "Transactions" => await query.CountAsync(cancellationToken),
            _ => await query.SumAsync(x => x.NetRetailSelling, cancellationToken)
        };
    }

    private static decimal Growth(decimal current, decimal previous)
        => previous <= 0 ? (current <= 0 ? 0 : 100) : System.Math.Round((current - previous) / previous * 100, 2);

    private static double pct(decimal a, decimal b)
    {
        if (b == 0) return 0;
        return (double)((a - b) / System.Math.Abs(b) * 100);
    }

    public async Task<object> GetKPIsAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var monthGroups = await qCurrent
            .GroupBy(x => new { x.YearNumber, x.MonthNumber })
            .Select(g => new {
                g.Key.YearNumber,
                g.Key.MonthNumber,
                NetSales = g.Sum(x => x.NetRetailSelling),
                NetDdl = g.Sum(x => x.NetRetailDdl),
                Discount = g.Sum(x => x.DiscountAmount),
                Count = (decimal)g.Count()
            })
            .OrderBy(x => x.YearNumber)
            .ThenBy(x => x.MonthNumber)
            .ToListAsync(cancellationToken);

        decimal totalValue = 0;
        int txnCount = 0;
        int activeMonths = monthGroups.Count;

        foreach (var mg in monthGroups)
        {
            totalValue += fc.Metric switch
            {
                "Net_Sales" => mg.NetSales,
                "Net_DDL" => mg.NetDdl,
                "Discount" => mg.Discount,
                "Transactions" => mg.Count,
                _ => mg.NetSales
            };
            txnCount += (int)mg.Count;
        }

        int uniqueLocs = await qCurrent.Select(x => x.Loc).Distinct().CountAsync(cancellationToken);
        decimal averageValue = activeMonths > 0 ? (totalValue / activeMonths) : 0;

        decimal cmValue = 0;
        decimal pmValue = 0;
        string latestMonthLabel = "LATEST";
        string prevMonthLabel = "prev";

        if (monthGroups.Count > 0)
        {
            var latestGroup = monthGroups.Last();
            var prevGroup = monthGroups.Count > 1 ? monthGroups[monthGroups.Count - 2] : null;

            latestMonthLabel = latestGroup.MonthNumber >= 1 && latestGroup.MonthNumber <= 12
                ? new DateTime(latestGroup.YearNumber ?? 2000, latestGroup.MonthNumber ?? 1, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)
                : latestGroup.MonthNumber?.ToString() ?? "—";

            cmValue = fc.Metric switch
            {
                "Net_Sales" => latestGroup.NetSales,
                "Net_DDL" => latestGroup.NetDdl,
                "Discount" => latestGroup.Discount,
                "Transactions" => latestGroup.Count,
                _ => latestGroup.NetSales
            };

            if (prevGroup != null)
            {
                prevMonthLabel = prevGroup.MonthNumber >= 1 && prevGroup.MonthNumber <= 12
                    ? new DateTime(prevGroup.YearNumber ?? 2000, prevGroup.MonthNumber ?? 1, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)
                    : prevGroup.MonthNumber?.ToString() ?? "—";

                pmValue = fc.Metric switch
                {
                    "Net_Sales" => prevGroup.NetSales,
                    "Net_DDL" => prevGroup.NetDdl,
                    "Discount" => prevGroup.Discount,
                    "Transactions" => prevGroup.Count,
                    _ => prevGroup.NetSales
                };
            }
        }

        var momGrowth = pmValue > 0 ? (double?)Growth(cmValue, pmValue) : null;

        var fiscalOrder = new List<int> { 4, 5, 6, 7, 8, 9, 10, 11, 12, 1, 2, 3 };
        
        int latestM = 3;
        if (fc.MonthNums.Count > 0)
        {
            latestM = fc.MonthNums.OrderByDescending(m => fiscalOrder.IndexOf(m)).First();
        }
        else if (monthGroups.Count > 0)
        {
            latestM = monthGroups.Last().MonthNumber ?? 3;
        }

        int calendarYear = latestM <= 3 ? fc.TargetYear + 1 : fc.TargetYear;
        int? maxDay = await db.Raws.AsNoTracking()
            .Where(x => !x.IsDeleted && x.ImportLogId > 0 && x.YearNumber == calendarYear && x.MonthNumber == latestM && x.Day.HasValue)
            .Select(x => x.Day)
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync(cancellationToken);

        int maxSelectedM = 3;
        if (fc.MonthNums.Count > 0)
        {
            maxSelectedM = fc.MonthNums.OrderByDescending(m => fiscalOrder.IndexOf(m)).First();
        }
        int maxIdx = fiscalOrder.IndexOf(maxSelectedM);
        var ytdMonths = fiscalOrder.Take(maxIdx + 1).ToList();

        var qYtdCurrent = await GetFilteredQueryWithMonthsAsync(fc, false, ytdMonths, cancellationToken, latestM, maxDay);
        var qYtdComp = await GetFilteredQueryWithMonthsAsync(fc, true, ytdMonths, cancellationToken, latestM, maxDay);

        decimal ytdSales = await ApplyMetricSumAsync(qYtdCurrent, fc.Metric, cancellationToken);
        decimal lytdSales = await ApplyMetricSumAsync(qYtdComp, fc.Metric, cancellationToken);
        decimal txnsGrowth = lytdSales > 0 ? (decimal)(ytdSales - lytdSales) / lytdSales * 100m : (ytdSales > 0 ? 100m : 0m);

        var qMtdCurrent = await GetFilteredQueryWithMonthsAsync(fc, false, new List<int> { latestM }, cancellationToken, latestM, maxDay);
        var qMtdComp = await GetFilteredQueryWithMonthsAsync(fc, true, new List<int> { latestM }, cancellationToken, latestM, maxDay);

        decimal mtdSales = await ApplyMetricSumAsync(qMtdCurrent, fc.Metric, cancellationToken);
        decimal lmtdSales = await ApplyMetricSumAsync(qMtdComp, fc.Metric, cancellationToken);
        decimal branchesGrowth = lmtdSales > 0 ? (decimal)(mtdSales - lmtdSales) / lmtdSales * 100m : (mtdSales > 0 ? 100m : 0m);

        return new
        {
            totalValue,
            averageValue,
            txnCount,
            uniqueLocs,
            activeMonths,
            latestMonth = latestMonthLabel,
            prevMonth = prevMonthLabel,
            latestMonthValue = cmValue,
            momGrowth,

            ytdSales,
            lytdSales,
            txnsGrowth = (double)txnsGrowth,

            mtdSales,
            lmtdSales,
            branchesGrowth = (double)branchesGrowth,

            latestMonthCompValue = lmtdSales,
            latestMonthYoYGrowth = (double)branchesGrowth
        };
    }

    public async Task<object> GetTrendAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        string? view,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        string trendView = view ?? "total";

        if (trendView == "total")
        {
            var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
            var trendCurrent = await qCurrent
                .GroupBy(x => x.MonthNumber)
                .Select(g => new {
                    MonthNumber = g.Key,
                    Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                     fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                     fc.Metric == "Discount" ? x.DiscountAmount :
                                     fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                })
                .ToListAsync(cancellationToken);

            var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);
            var trendComp = await qComp
                .GroupBy(x => x.MonthNumber)
                .Select(g => new {
                    MonthNumber = g.Key,
                    Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                     fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                     fc.Metric == "Discount" ? x.DiscountAmount :
                                     fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                })
                .ToListAsync(cancellationToken);

            var currTrend = MO_ORDER.Select(mo => {
                var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                return trendCurrent.FirstOrDefault(t => t.MonthNumber == mNum)?.Value ?? 0m;
            }).ToList();

            var compTrend = MO_ORDER.Select(mo => {
                var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                return trendComp.FirstOrDefault(t => t.MonthNumber == mNum)?.Value ?? 0m;
            }).ToList();

            return new { categories = MO_ORDER, series = new[] {
                new { name = fc.IsCurrentYear ? $"FY {fc.AnchorYear}-{fc.AnchorYear + 1} (Current)" : $"FY {fc.AnchorYear - 1}-{fc.AnchorYear} (Last Year)", data = currTrend },
                new { name = fc.IsCurrentYear ? $"FY {fc.AnchorYear - 1}-{fc.AnchorYear} (Last Year)" : $"FY {fc.AnchorYear}-{fc.AnchorYear + 1} (Current)", data = compTrend }
            }};
        }
        else if (trendView == "stacked")
        {
            var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
            var grouped = await qCurrent
                .GroupBy(x => new {
                    PartyType = x.PartyType ?? "INDEPENDENT WORKSHOP",
                    x.MonthNumber
                })
                .Select(g => new {
                    PartyType = g.Key.PartyType.Trim().ToUpperInvariant(),
                    g.Key.MonthNumber,
                    Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                     fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                     fc.Metric == "Discount" ? x.DiscountAmount :
                                     fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                })
                .ToListAsync(cancellationToken);

            var parties = grouped.Select(x => x.PartyType).Distinct().OrderBy(p => p).ToList();
            var series = parties.Select(p => new
            {
                name = p,
                data = MO_ORDER.Select(mo => {
                    var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                    return grouped.FirstOrDefault(x => x.PartyType == p && x.MonthNumber == mNum)?.Value ?? 0m;
                }).ToList()
            }).ToList();

            return new { categories = MO_ORDER, series };
        }
        else if (trendView == "category")
        {
            var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
            var grouped = await qCurrent
                .GroupBy(x => new {
                    Category = x.PartCategoryCode ?? "AA",
                    x.MonthNumber
                })
                .Select(g => new {
                    Category = g.Key.Category.Trim().ToUpperInvariant(),
                    g.Key.MonthNumber,
                    Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                     fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                     fc.Metric == "Discount" ? x.DiscountAmount :
                                     fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                })
                .ToListAsync(cancellationToken);

            var cats = grouped.Select(x => x.Category).Distinct().OrderBy(c => c).ToList();
            var series = cats.Select(c => new
            {
                name = c,
                data = MO_ORDER.Select(mo => {
                    var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                    return grouped.FirstOrDefault(x => x.Category == c && x.MonthNumber == mNum)?.Value ?? 0m;
                }).ToList()
            }).ToList();

            return new { categories = MO_ORDER, series };
        }
        else
        {
            var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
            var grouped = await qCurrent
                .GroupBy(x => new {
                    DealerSubType = x.DealerSubType ?? "AW",
                    x.MonthNumber
                })
                .Select(g => new {
                    DealerSubType = g.Key.DealerSubType.Trim().ToUpperInvariant(),
                    g.Key.MonthNumber,
                    Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                     fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                     fc.Metric == "Discount" ? x.DiscountAmount :
                                     fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                })
                .ToListAsync(cancellationToken);

            var dsts = grouped.Select(x => x.DealerSubType).Distinct().OrderBy(d => d).ToList();
            var series = dsts.Select(d => new
            {
                name = d,
                data = MO_ORDER.Select(mo => {
                    var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                    return grouped.FirstOrDefault(x => x.DealerSubType == d && x.MonthNumber == mNum)?.Value ?? 0m;
                }).ToList()
            }).ToList();

            return new { categories = MO_ORDER, series };
        }
    }

    public async Task<object> GetCategoryMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var grouped = await qCurrent
            .GroupBy(x => x.PartCategoryCode ?? "Other")
            .Select(g => new
            {
                Category = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .OrderByDescending(x => x.Value)
            .ToListAsync(cancellationToken);

        var totalVal = grouped.Sum(x => x.Value);
        var labels = grouped.Select(x => x.Category.Trim().ToUpperInvariant()).ToList();
        var vals = grouped.Select(x => System.Math.Round(x.Value, 0)).ToList();

        var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);
        var compTotalVal = await ApplyMetricSumAsync(qComp, fc.Metric, cancellationToken);
        var diff = totalVal - compTotalVal;
        var pctVal = compTotalVal > 0 ? (double?)pct(totalVal, compTotalVal) : null;

        return new { labels, values = vals, total = totalVal, diff, pct = pctVal };
    }

    public async Task<object> GetPartyMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
        var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);

        var byPartyCurrent = await qCurrent
            .GroupBy(x => x.PartyType ?? "INDEPENDENT WORKSHOP")
            .Select(g => new
            {
                PartyType = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var byPartyComp = await qComp
            .GroupBy(x => x.PartyType ?? "INDEPENDENT WORKSHOP")
            .Select(g => new
            {
                PartyType = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var currentDict = byPartyCurrent.ToDictionary(x => x.PartyType.Trim().ToUpperInvariant(), x => x.Value);
        var compDict = byPartyComp.ToDictionary(x => x.PartyType.Trim().ToUpperInvariant(), x => x.Value);

        var allPartyTypes = currentDict.Keys.Union(compDict.Keys).OrderBy(t => t).ToList();

        var labels = new List<string>();
        var currentValues = new List<decimal>();
        var growthPercentages = new List<double>();

        foreach (var pt in allPartyTypes)
        {
            currentDict.TryGetValue(pt, out decimal curVal);
            compDict.TryGetValue(pt, out decimal compVal);

            double growth = 0;
            if (compVal > 0)
            {
                growth = (double)((curVal - compVal) / compVal * 100m);
            }
            else if (curVal > 0)
            {
                growth = 100.0;
            }

            labels.Add(pt);
            currentValues.Add(curVal);
            growthPercentages.Add(System.Math.Round(growth, 2));
        }

        return new
        {
            labels,
            values = currentValues,
            growth = growthPercentages
        };
    }

    public async Task<object> GetQuarterSummaryAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);
        var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);

        var groupedCurrent = await qCurrent
            .GroupBy(x => x.Quarter ?? "")
            .Select(g => new {
                Quarter = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var groupedComp = await qComp
            .GroupBy(x => x.Quarter ?? "")
            .Select(g => new {
                Quarter = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var quartersArray = new[] { "Q1", "Q2", "Q3", "Q4" };
        var valsCurrent = quartersArray.Select(q => groupedCurrent.FirstOrDefault(x => x.Quarter == q)?.Value ?? 0m).ToList();
        var valsComp = quartersArray.Select(q => groupedComp.FirstOrDefault(x => x.Quarter == q)?.Value ?? 0m).ToList();

        var series = new[]
        {
            new { name = fc.IsCurrentYear ? $"FY {fc.AnchorYear}-{fc.AnchorYear + 1} (Current)" : $"FY {fc.AnchorYear - 1}-{fc.AnchorYear} (Last Year)", data = valsCurrent },
            new { name = fc.IsCurrentYear ? $"FY {fc.AnchorYear - 1}-{fc.AnchorYear} (Last Year)" : $"FY {fc.AnchorYear}-{fc.AnchorYear + 1} (Current)", data = valsComp }
        };

        return new { labels = quartersArray, series = series };
    }

    public async Task<object> GetLocationRankingAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var grouped = await (from r in qCurrent
                             join b in db.Branches on r.Loc equals b.Code into bj
                             from b in bj.DefaultIfEmpty()
                             group r by new { Code = r.Loc, Name = b != null ? b.Name : r.Loc } into g
                             select new
                             {
                                 Loc = g.Key.Code,
                                 Name = g.Key.Name,
                                 Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                                  fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                                  fc.Metric == "Discount" ? x.DiscountAmount :
                                                  fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                             })
                             .OrderByDescending(x => x.Value)
                             .ToListAsync(cancellationToken);

        return grouped;
    }

    public async Task<object> GetComparisonAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        string? cmp,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var grouped = await qCurrent
            .GroupBy(x => new { x.PartyType, x.MonthNumber, x.Quarter })
            .Select(g => new {
                PartyType = g.Key.PartyType ?? "INDEPENDENT WORKSHOP",
                MonthNumber = g.Key.MonthNumber,
                Quarter = g.Key.Quarter ?? "",
                Value = g.Sum(x => x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        string compareMode = cmp ?? "none";
        var parties = grouped.Select(x => x.PartyType.Trim().ToUpperInvariant()).Distinct().OrderBy(p => p).ToList();

        if (compareMode == "none")
        {
            var activeMonths = MO_ORDER.Where(mo => {
                var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                return grouped.Any(x => x.MonthNumber == mNum);
            }).ToList();
            var recentMonths = activeMonths.Count > 5 ? activeMonths.Skip(activeMonths.Count - 5).ToList() : activeMonths;

            var rows = parties.Select(p => {
                var monthVals = recentMonths.Select(mo => {
                    var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                    return grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p && x.MonthNumber == mNum).Sum(x => x.Value);
                }).ToList();
                var total = grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p).Sum(x => x.Value);
                var avg = activeMonths.Count > 0 ? (total / activeMonths.Count) : 0;
                return new { partyType = p, values = monthVals, total, avg };
            }).ToList();

            return new { type = "none", headers = recentMonths, rows };
        }
        else
        {
            string currHeader = "";
            string prevHeader = "";

            var activeMonths = MO_ORDER.Where(mo => {
                var mNum = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, mo) + 1;
                return grouped.Any(x => x.MonthNumber == mNum);
            }).ToList();
            
            var rows = parties.Select(p => {
                decimal currVal = 0;
                decimal prevVal = 0;

                if (compareMode == "mom")
                {
                    currHeader = activeMonths.LastOrDefault() ?? "Current Month";
                    var currIdx = MO_ORDER.IndexOf(currHeader);
                    prevHeader = currIdx > 0 ? MO_ORDER[currIdx - 1] : "Previous Month";

                    var currM = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, currHeader) + 1;
                    var prevM = Array.IndexOf(new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }, prevHeader) + 1;

                    currVal = grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p && x.MonthNumber == currM).Sum(x => x.Value);
                    prevVal = grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p && x.MonthNumber == prevM).Sum(x => x.Value);
                }
                else
                {
                    var activeQuarters = grouped.Select(x => x.Quarter).Distinct().Where(q => !string.IsNullOrEmpty(q)).OrderBy(q => q).ToList();
                    currHeader = activeQuarters.LastOrDefault() ?? "Q4";
                    int qNum = int.Parse(currHeader.Replace("Q", ""));
                    prevHeader = qNum > 1 ? "Q" + (qNum - 1) : "Q4";

                    currVal = grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p && x.Quarter == currHeader).Sum(x => x.Value);
                    prevVal = grouped.Where(x => x.PartyType.Trim().ToUpperInvariant() == p && x.Quarter == prevHeader).Sum(x => x.Value);
                }

                decimal diff = currVal - prevVal;
                decimal? pctVal = prevVal > 0 ? (decimal?)Growth(currVal, prevVal) : null;

                return new { partyType = p, current = currVal, previous = prevVal, diff, pct = pctVal };
            }).ToList();

            return new { type = compareMode, headers = new[] { currHeader, prevHeader }, rows };
        }
    }

    public async Task<object> GetConsigneeSummaryAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var grouped = await qCurrent
            .GroupBy(x => new { x.Loc, x.MonthNumber })
            .Select(g => new {
                Loc = g.Key.Loc ?? "",
                MonthNumber = g.Key.MonthNumber,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var consigneeMap = await db.Branches
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Consignee != null)
            .Select(b => new { b.Code, b.Consignee })
            .ToDictionaryAsync(b => b.Code, b => b.Consignee!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var consigneeTotals = grouped
            .GroupBy(x => consigneeMap.TryGetValue(x.Loc, out var cons) ? cons : "-")
            .Select(g => new {
                Consignee = g.Key,
                Total = g.Sum(x => x.Value)
            })
            .OrderByDescending(x => x.Total)
            .Take(5)
            .ToList();

        var topConsignees = consigneeTotals.Select(x => x.Consignee).ToList();

        var series = topConsignees.Select(cons => new
        {
            name = cons,
            data = fc.MonthNums.Select(m => System.Math.Round(grouped.Where(x => (consigneeMap.TryGetValue(x.Loc, out var c) ? c : "-") == cons && x.MonthNumber == m).Sum(x => x.Value), 0)).ToList()
        }).ToList();

        return new { categories = fc.MonthNums.Select(m => MO_ORDER[m - 1]).ToList(), series };
    }

    public async Task<object> GetConsigneeMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var consigneeMap = await db.Branches
            .AsNoTracking()
            .Where(b => !b.IsDeleted && b.Consignee != null)
            .Select(b => new { b.Code, b.Consignee })
            .ToDictionaryAsync(b => b.Code, b => b.Consignee!, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var grouped = await qCurrent
            .GroupBy(x => x.Loc ?? "")
            .Select(g => new
            {
                Loc = g.Key,
                Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                 fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                 fc.Metric == "Discount" ? x.DiscountAmount :
                                 fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
            })
            .ToListAsync(cancellationToken);

        var consigneeTotals = grouped
            .GroupBy(x => consigneeMap.TryGetValue(x.Loc, out var cons) ? cons : "Other")
            .Select(g => new {
                Consignee = g.Key,
                Value = g.Sum(x => x.Value)
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        var totalVal = consigneeTotals.Sum(x => x.Value);
        var labels = consigneeTotals.Select(x => x.Consignee).ToList();
        var vals = consigneeTotals.Select(x => System.Math.Round(x.Value, 0)).ToList();

        var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);
        var compTotalVal = await ApplyMetricSumAsync(qComp, fc.Metric, cancellationToken);
        var diff = totalVal - compTotalVal;
        var pctVal = compTotalVal > 0 ? (double?)pct(totalVal, compTotalVal) : null;

        return new { labels, values = vals, total = totalVal, diff, pct = pctVal };
    }

    public async Task<object> GetDealerSubTypeMixAsync(
        string? yr,
        string? met,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? consignees,
        string? dealerSubTypes,
        string? locations,
        CancellationToken cancellationToken)
    {
        var fc = await ParseFiltersAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
        var qCurrent = await GetFilteredQueryAsync(fc, false, cancellationToken);

        var grouped = await (from r in qCurrent
                             join b in db.Branches on r.Loc equals b.Code into bj
                             from b in bj.DefaultIfEmpty()
                             group r by new { BranchType = b != null && b.BranchType != null ? b.BranchType : "AW" } into g
                             select new
                             {
                                 DealerSubType = g.Key.BranchType,
                                 Value = g.Sum(x => fc.Metric == "Net_Sales" ? x.NetRetailSelling :
                                                  fc.Metric == "Net_DDL" ? x.NetRetailDdl :
                                                  fc.Metric == "Discount" ? x.DiscountAmount :
                                                  fc.Metric == "Transactions" ? 1m : x.NetRetailSelling)
                             }).ToListAsync(cancellationToken);

        var validTypes = new[] { "AW", "MW", "RO" };

        var dealerSubTotals = validTypes.Select(t => new
        {
            DealerSubType = t,
            Value = grouped.Where(x => x.DealerSubType != null && x.DealerSubType.Trim().Equals(t, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Value) 
                    + (t == "AW" ? grouped.Where(x => x.DealerSubType == null || !validTypes.Contains(x.DealerSubType.Trim(), StringComparer.OrdinalIgnoreCase)).Sum(x => x.Value) : 0)
        }).ToList();

        var totalVal = dealerSubTotals.Sum(x => x.Value);
        var labels = dealerSubTotals.Select(x => x.DealerSubType).ToList();
        var vals = dealerSubTotals.Select(x => System.Math.Round(x.Value, 0)).ToList();

        var qComp = await GetFilteredQueryAsync(fc, true, cancellationToken);
        var compTotalVal = await ApplyMetricSumAsync(qComp, fc.Metric, cancellationToken);
        var diff = totalVal - compTotalVal;
        var pctVal = compTotalVal > 0 ? (double?)pct(totalVal, compTotalVal) : null;

        return new { labels, values = vals, total = totalVal, diff, pct = pctVal };
    }
}
