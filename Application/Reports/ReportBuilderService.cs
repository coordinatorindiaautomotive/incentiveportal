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
using IncentivePortal.Controllers;

namespace IncentivePortal.Services;

public interface IReportBuilderService
{
    Task<IncentiveRegisterViewModel> BuildIncentiveRegisterAsync(IncentiveRegisterFilter filter, CancellationToken cancellationToken);
    Task<List<OutstandingMasterRow>> GetOutstandingMasterAsync(int? month = null, int? year = null, CancellationToken cancellationToken = default);
    Task<PerformanceReportsViewModel> BuildPerformanceReportsAsync(
        List<string>? dealerType,
        List<int>? branchId,
        List<string>? partyCode,
        List<string>? categories,
        int? month,
        int? year,
        CancellationToken cancellationToken);
    Task<object> GetDealerSalesAsync(
        int targetYear,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? locations,
        string? dealerSubTypes,
        string? search,
        string? limit,
        CancellationToken cancellationToken);

    Task<TargetVsAchievementViewModel> GetTargetVsAchievementAsync(int? month, int? year, string? branchName, string? partyType, string? partCategoryCode, CancellationToken cancellationToken);
}


public sealed class ReportBuilderService(IncentiveDbContext db, ICurrentUser currentUser) : IReportBuilderService
{
    public async Task<IncentiveRegisterViewModel> BuildIncentiveRegisterAsync(IncentiveRegisterFilter filter, CancellationToken cancellationToken)
    {
        var query = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted).AsQueryable();

        // Enforce branch-level and salesman-level user restrictions
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
            var userBranchId = currentUser.BranchId.Value;
            var allowedPartyCodes = await db.Parties
                .Where(p => p.BranchId == userBranchId && !p.IsDeleted)
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => allowedPartyCodes.Contains(x.PartyCode));
        }

        if (filter.SelectedPartyCodes != null && filter.SelectedPartyCodes.Count > 0)
        {
            query = query.Where(x => filter.SelectedPartyCodes.Contains(x.PartyCode));
        }
        else if (!string.IsNullOrWhiteSpace(filter.PartyCode))
        {
            query = query.Where(x => x.PartyCode == filter.PartyCode);
        }

        // Apply multiple SelectedPeriods filter
        if (filter.SelectedPeriods != null && filter.SelectedPeriods.Count > 0)
        {
            var periodFilters = filter.SelectedPeriods
                .Select(p => p.Split('-'))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
                .Select(parts => new { Year = int.Parse(parts[0]), Month = int.Parse(parts[1]) })
                .ToList();

            if (periodFilters.Count > 0)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(SsIncentive), "x");
                System.Linq.Expressions.Expression? body = null;
                foreach (var pf in periodFilters)
                {
                    var yearExpr = System.Linq.Expressions.Expression.Equal(
                        System.Linq.Expressions.Expression.Property(parameter, nameof(SsIncentive.Year)),
                        System.Linq.Expressions.Expression.Constant(pf.Year));
                    var monthExpr = System.Linq.Expressions.Expression.Equal(
                        System.Linq.Expressions.Expression.Property(parameter, nameof(SsIncentive.Month)),
                        System.Linq.Expressions.Expression.Constant(pf.Month));
                    var andExpr = System.Linq.Expressions.Expression.AndAlso(yearExpr, monthExpr);

                    body = body == null ? andExpr : System.Linq.Expressions.Expression.OrElse(body, andExpr);
                }
                if (body != null)
                {
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<SsIncentive, bool>>(body, parameter);
                    query = query.Where(lambda);
                }
            }
        }
        else
        {
            if (filter.Month.HasValue)
                query = query.Where(x => x.Month == filter.Month.Value);
            if (filter.Year.HasValue)
                query = query.Where(x => x.Year == filter.Year.Value);
        }

        // Apply multiple SelectedPaymentPeriods filter (Payment Month)
        if (filter.SelectedPaymentPeriods != null && filter.SelectedPaymentPeriods.Count > 0)
        {
            var paymentFilters = filter.SelectedPaymentPeriods
                .Select(p => p.Split('-'))
                .Where(parts => parts.Length == 2 && int.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
                .Select(parts => new { Year = int.Parse(parts[0]), Month = int.Parse(parts[1]) })
                .ToList();

            if (paymentFilters.Count > 0)
            {
                var parameter = System.Linq.Expressions.Expression.Parameter(typeof(SsIncentive), "x");
                System.Linq.Expressions.Expression? body = null;
                foreach (var pf in paymentFilters)
                {
                    var hasDateExpr = System.Linq.Expressions.Expression.NotEqual(
                        System.Linq.Expressions.Expression.Property(parameter, nameof(SsIncentive.PaymentDate)),
                        System.Linq.Expressions.Expression.Constant(null, typeof(DateTime?)));
                        
                    var dateProp = System.Linq.Expressions.Expression.Property(
                        System.Linq.Expressions.Expression.Property(parameter, nameof(SsIncentive.PaymentDate)),
                        "Value");
                        
                    var yearExpr = System.Linq.Expressions.Expression.Equal(
                        System.Linq.Expressions.Expression.Property(dateProp, nameof(DateTime.Year)),
                        System.Linq.Expressions.Expression.Constant(pf.Year));
                    var monthExpr = System.Linq.Expressions.Expression.Equal(
                        System.Linq.Expressions.Expression.Property(dateProp, nameof(DateTime.Month)),
                        System.Linq.Expressions.Expression.Constant(pf.Month));
                    var andExpr = System.Linq.Expressions.Expression.AndAlso(hasDateExpr, System.Linq.Expressions.Expression.AndAlso(yearExpr, monthExpr));

                    body = body == null ? andExpr : System.Linq.Expressions.Expression.OrElse(body, andExpr);
                }
                if (body != null)
                {
                    var lambda = System.Linq.Expressions.Expression.Lambda<Func<SsIncentive, bool>>(body, parameter);
                    query = query.Where(lambda);
                }
            }
        }

        var dbRows = await query
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ThenBy(x => x.PartyName)
            .Take(5000)
            .ToListAsync(cancellationToken);

        var partyCodes = dbRows.Select(x => x.PartyCode).Distinct().ToList();
        var bankDetailsMap = await db.BankDetails
            .AsNoTracking()
            .Where(b => b.ApprovalStatus == "Approved" && !b.IsDeleted)
            .Include(b => b.Party)
            .Where(b => partyCodes.Contains(b.Party.PartyCode))
            .ToDictionaryAsync(b => b.Party.PartyCode, b => b, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Fix: scope to only the party codes present in the result set.
        // Previously loaded ALL non-deleted parties — unnecessary full-table scan.
        // Step 1: Find all potential parties and load their Original Code mapping
        var partyInfoMap = await db.Parties
            .AsNoTracking()
            .Where(p => !p.IsDeleted && partyCodes.Contains(p.PartyCode))
            .ToDictionaryAsync(p => p.PartyCode, p => p.OriginalPartyCode, StringComparer.OrdinalIgnoreCase, cancellationToken);

        // Step 2: Get resolved original party codes for the rows in the result set
        var resolvedOriginalCodes = dbRows.Select(x => {
            partyInfoMap.TryGetValue(x.PartyCode, out var origCode);
            return !string.IsNullOrEmpty(origCode) ? origCode : x.PartyCode;
        }).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        // Step 3: Load original party records (with branch) and executive mappings for the resolved codes
        var originalPartyMap = await db.Parties
            .AsNoTracking()
            .Include(p => p.Branch)
            .Where(p => !p.IsDeleted && (resolvedOriginalCodes.Contains(p.PartyCode) || partyCodes.Contains(p.PartyCode)))
            .ToDictionaryAsync(p => p.PartyCode, p => p, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var executiveMap = await db.PartyExecutiveMappings
            .AsNoTracking()
            .Where(m => resolvedOriginalCodes.Contains(m.PartyCode) || partyCodes.Contains(m.PartyCode))
            .ToDictionaryAsync(m => m.PartyCode, m => m, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rows = dbRows.Select(x => {
            bankDetailsMap.TryGetValue(x.PartyCode, out var bank);
            
            // Get resolved original code
            partyInfoMap.TryGetValue(x.PartyCode, out var origCode);
            var resolvedCode = !string.IsNullOrEmpty(origCode) ? origCode : x.PartyCode;

            // Fetch info using resolved code, fallback to raw code if not found
            originalPartyMap.TryGetValue(resolvedCode, out var origParty);
            if (origParty == null)
            {
                originalPartyMap.TryGetValue(x.PartyCode, out origParty);
            }
            
            executiveMap.TryGetValue(resolvedCode, out var execMapping);
            if (execMapping == null)
            {
                executiveMap.TryGetValue(x.PartyCode, out execMapping);
            }

            var branchCode = origParty?.Branch?.Code ?? "-";
            var partyNameResolved = origParty != null ? origParty.PartyName : x.PartyName;
            var execName = execMapping != null ? execMapping.ExecutiveName : "-";

            return new IncentiveRegisterRow(
                new DateTime(x.Year, x.Month, 1).ToString("MMMM yyyy"),
                x.Month,
                x.Year,
                PartyCode: resolvedCode, // ONLY show original code
                PartyName: partyNameResolved, // Show original party name
                x.SaleValue,
                x.AchievementPercent / 100m,
                x.OnBillDiscount,
                x.AchievementPercent,
                x.GrossIncentive,
                x.TdsAmount,
                x.NetTransferAmount,
                x.TransferredAmount,
                x.CreatedAt,
                x.PaymentDate,
                x.PaymentStatus,
                x.UTRNumber,
                bank?.AccountNumber ?? "-",
                bank?.IFSC ?? "-",
                bank?.AccountHolder ?? "-",
                BranchCode: branchCode,
                OriginalPartyCode: resolvedCode,
                SalesExecutive: execName
            );
        }).ToList();

        // Populate metadata options from the SsIncentives query
        var activePartyQuery = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted).AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var userBranchId = currentUser.BranchId.Value;
            var allowedCodes = await db.Parties
                .Where(p => p.BranchId == userBranchId && !p.IsDeleted)
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            activePartyQuery = activePartyQuery.Where(x => allowedCodes.Contains(x.PartyCode));
        }

        var activePartyCodes = await activePartyQuery
            .Select(x => x.PartyCode)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        // Branch isolated Party query
        var allParties = await db.Parties
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new PartyLookupItem(x.Id, x.PartyCode, x.PartyName, x.OriginalPartyCode))
            .ToListAsync(cancellationToken);

        // Fetch partyIds with approved bank details to filter them out of updates list
        var partiesWithBank = await db.BankDetails
            .Where(x => x.ApprovalStatus == "Approved" && !x.IsDeleted)
            .Select(x => x.PartyId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var partiesWithoutBank = allParties
            .Where(x => !partiesWithBank.Contains(x.Id))
            .ToList();

        var periodsQuery = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted).AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var userBranchId = currentUser.BranchId.Value;
            var allowedCodes = await db.Parties
                .Where(p => p.BranchId == userBranchId && !p.IsDeleted)
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            periodsQuery = periodsQuery.Where(x => allowedCodes.Contains(x.PartyCode));
        }

        var periods = await periodsQuery
            .GroupBy(x => new { Month = x.Month, Year = x.Year })
            .OrderByDescending(x => x.Key.Year)
            .ThenByDescending(x => x.Key.Month)
            .Select(x => new ValueTuple<int, int, string>(x.Key.Month, x.Key.Year, ""))
            .ToListAsync(cancellationToken);

        var paymentPeriodsQuery = db.SsIncentives.AsNoTracking().Where(x => !x.IsDeleted && x.PaymentDate != null).AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var userBranchId = currentUser.BranchId.Value;
            var allowedCodes = await db.Parties
                .Where(p => p.BranchId == userBranchId && !p.IsDeleted)
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            paymentPeriodsQuery = paymentPeriodsQuery.Where(x => allowedCodes.Contains(x.PartyCode));
        }

        var paymentPeriods = await paymentPeriodsQuery
            .GroupBy(x => new { Year = x.PaymentDate!.Value.Year, Month = x.PaymentDate!.Value.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new ValueTuple<int, int, string>(g.Key.Month, g.Key.Year, ""))
            .ToListAsync(cancellationToken);

        var colConfigs = await db.ReportColumnConfigs
            .Where(c => c.ReportName == "IncentiveRegister" && !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(cancellationToken);

        return new IncentiveRegisterViewModel
        {
            Filter = filter,
            PartyCodes = activePartyCodes,
            Parties = partiesWithoutBank,
            Periods = periods.Select(x => (x.Item1, x.Item2, new DateTime(x.Item2, x.Item1, 1).ToString("MMM yyyy"))).ToList(),
            PaymentPeriods = paymentPeriods.Select(x => (x.Item1, x.Item2, new DateTime(x.Item2, x.Item1, 1).ToString("MMM yyyy"))).ToList(),
            Rows = rows,
            ColumnConfigs = colConfigs
        };
    }

    public async Task<List<OutstandingMasterRow>> GetOutstandingMasterAsync(int? month = null, int? year = null, CancellationToken cancellationToken = default)
    {
        var partiesQuery = db.Parties.AsNoTracking().Include(p => p.Branch).Where(p => !p.IsDeleted).AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            partiesQuery = partiesQuery.Where(p => p.BranchId == currentUser.BranchId.Value);
        }

        var parties = await partiesQuery.ToListAsync(cancellationToken);
        var partyCodes = parties.Select(p => p.PartyCode).ToList();

        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear = year ?? DateTime.UtcNow.Year;

        if (!month.HasValue || !year.HasValue)
        {
            var latestPeriod = await db.DealerOutstandings
                .AsNoTracking()
                .Where(o => !o.IsDeleted && partyCodes.Contains(o.PartyCode))
                .OrderByDescending(o => o.Year)
                .ThenByDescending(o => o.Month)
                .Select(o => new { o.Year, o.Month })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestPeriod != null)
            {
                targetMonth = latestPeriod.Month;
                targetYear = latestPeriod.Year;
            }
        }

        var outstandings = await db.DealerOutstandings
            .AsNoTracking()
            .Where(o => o.Year == targetYear && o.Month == targetMonth && !o.IsDeleted && partyCodes.Contains(o.PartyCode))
            .ToDictionaryAsync(o => o.PartyCode, o => o, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var saleValues = await db.SsIncentives
            .AsNoTracking()
            .Where(s => s.Year == targetYear && s.Month == targetMonth && !s.IsDeleted && partyCodes.Contains(s.PartyCode))
            .ToDictionaryAsync(s => s.PartyCode, s => s.SaleValue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var viewModel = parties.Select(p => {
            outstandings.TryGetValue(p.PartyCode, out var outs);
            saleValues.TryGetValue(p.PartyCode, out var saleVal);
            return new OutstandingMasterRow(
                p.PartyCode,
                p.PartyName,
                p.Branch?.Code ?? "HO",
                p.Branch?.Name ?? "Head Office",
                outs?.Year ?? targetYear,
                outs?.Month ?? targetMonth,
                saleVal,
                outs?.Outstanding ?? 0m,
                outs?.UpdatedAt ?? outs?.CreatedAt ?? p.UpdatedAt ?? p.CreatedAt,
                outs?.OutstandingLess7Days,
                outs?.Outstanding7To14Days,
                outs?.Outstanding14To21Days,
                outs?.Outstanding21To28Days,
                outs?.Outstanding28To35Days,
                outs?.Outstanding35To50Days,
                outs?.Outstanding50To80Days,
                outs?.OutstandingMore80Days
            );
        })
        .Where(r => r.Outstanding != 0m)
        .OrderByDescending(r => r.Outstanding)
        .ToList();

        return viewModel;
    }

    public async Task<PerformanceReportsViewModel> BuildPerformanceReportsAsync(
        List<string>? dealerType,
        List<int>? branchId,
        List<string>? partyCode,
        List<string>? categories,
        int? month,
        int? year,
        CancellationToken cancellationToken)
    {
        var query = db.SsIncentives.Where(x => !x.IsDeleted && x.ImportLogId > 0);

        // Enforce branch-level isolation if branch manager
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var branchParties = await db.Parties
                .Where(x => x.BranchId == currentUser.BranchId.Value)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => branchParties.Contains(x.PartyCode));
        }

        // Apply dynamic Category multi-select filter via branch allowed categories mapping
        if (categories != null && categories.Count > 0)
        {
            var branchCodes = new List<string>();
            var allBranches = await db.Branches.Where(b => !b.IsDeleted).ToListAsync(cancellationToken);
            foreach (var br in allBranches)
            {
                if (!string.IsNullOrEmpty(br.AllowedCategories))
                {
                    var cats = br.AllowedCategories.Split(',').Select(c => c.Trim());
                    if (cats.Any(c => categories.Contains(c, StringComparer.OrdinalIgnoreCase)))
                    {
                        branchCodes.Add(br.Code);
                    }
                }
            }

            var matchingPartyCodes = await db.Parties
                .Where(p => branchCodes.Contains(p.Branch.Code))
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);

            query = query.Where(x => matchingPartyCodes.Contains(x.PartyCode));
        }

        // Apply dealerTypes multi-select filter
        if (dealerType != null && dealerType.Count > 0)
        {
            var partyCodesForTypes = await db.Parties
                .Where(p => dealerType.Contains(p.DealerType))
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => partyCodesForTypes.Contains(x.PartyCode));
        }

        // Apply branchIds multi-select filter
        if (branchId != null && branchId.Count > 0)
        {
            var partyCodesForBranches = await db.Parties
                .Where(p => branchId.Contains(p.BranchId))
                .Select(p => p.PartyCode)
                .ToListAsync(cancellationToken);
            query = query.Where(x => partyCodesForBranches.Contains(x.PartyCode));
        }

        // Apply partyCodes multi-select filter
        if (partyCode != null && partyCode.Count > 0)
        {
            query = query.Where(x => partyCode.Contains(x.PartyCode));
        }

        // Determine target period for comparative metrics
        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear = year ?? DateTime.UtcNow.Year;

        if (!month.HasValue || !year.HasValue)
        {
            var latestLedger = await query
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestLedger != null)
            {
                targetMonth = latestLedger.Month;
                targetYear = latestLedger.Year;
            }
        }

        // Get comparative stats for CM, LM, LY
        int lmMonth = targetMonth == 1 ? 12 : targetMonth - 1;
        int lmYear = targetMonth == 1 ? targetYear - 1 : targetYear;
        int lyMonth = targetMonth;
        int lyYear = targetYear - 1;

        var cmStats = await query.Where(x => x.Year == targetYear && x.Month == targetMonth)
            .GroupBy(x => 1)
            .Select(g => new { Sales = g.Sum(x => x.SaleValue), Gross = g.Sum(x => x.GrossIncentive), Net = g.Sum(x => x.NetTransferAmount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var lmStats = await query.Where(x => x.Year == lmYear && x.Month == lmMonth)
            .GroupBy(x => 1)
            .Select(g => new { Sales = g.Sum(x => x.SaleValue), Gross = g.Sum(x => x.GrossIncentive), Net = g.Sum(x => x.NetTransferAmount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var lyStats = await query.Where(x => x.Year == lyYear && x.Month == lyMonth)
            .GroupBy(x => 1)
            .Select(g => new { Sales = g.Sum(x => x.SaleValue), Gross = g.Sum(x => x.GrossIncentive), Net = g.Sum(x => x.NetTransferAmount), Count = g.Count() })
            .FirstOrDefaultAsync(cancellationToken);

        var cmSales = cmStats?.Sales ?? 0m;
        var cmGross = cmStats?.Gross ?? 0m;
        var cmNet = cmStats?.Net ?? 0m;
        var cmCount = cmStats?.Count ?? 0;

        var lmSales = lmStats?.Sales ?? 0m;
        var lmGross = lmStats?.Gross ?? 0m;

        var lySales = lyStats?.Sales ?? 0m;
        var lyGross = lyStats?.Gross ?? 0m;

        decimal? salesMoM = lmSales > 0 ? ((cmSales - lmSales) / lmSales) * 100 : null;
        decimal? incentiveMoM = lmGross > 0 ? ((cmGross - lmGross) / lmGross) * 100 : null;

        decimal? salesYoY = lySales > 0 ? ((cmSales - lySales) / lySales) * 100 : null;
        decimal? incentiveYoY = lyGross > 0 ? ((cmGross - lyGross) / lyGross) * 100 : null;

        var yearSummary = await query
            .GroupBy(x => x.Year)
            .Select(g => new YearSummaryItem
            {
                Year = g.Key,
                TotalSales = g.Sum(x => x.SaleValue),
                TotalGross = g.Sum(x => x.GrossIncentive),
                TotalNet = g.Sum(x => x.NetTransferAmount),
                RecordCount = g.Count()
            })
            .OrderByDescending(x => x.Year)
            .ToListAsync(cancellationToken);

        var monthSummary = await query
            .GroupBy(x => new { x.Year, x.Month })
            .Select(g => new MonthSummaryItem
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalSales = g.Sum(x => x.SaleValue),
                TotalGross = g.Sum(x => x.GrossIncentive),
                TotalNet = g.Sum(x => x.NetTransferAmount),
                RecordCount = g.Count()
            })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        var employeeHistory = new List<EmployeeHistoryItem>();

        var dealerHistory = await query
            .GroupBy(x => new { x.PartyCode, PartyName = x.PartyName ?? "" })
            .Select(g => new DealerHistoryItem
            {
                PartyCode = g.Key.PartyCode,
                PartyName = g.Key.PartyName,
                TotalSales = g.Sum(x => x.SaleValue),
                TotalGross = g.Sum(x => x.GrossIncentive),
                TotalNet = g.Sum(x => x.NetTransferAmount)
            })
            .OrderByDescending(x => x.TotalSales)
            .ToListAsync(cancellationToken);

        var slabPerformance = new List<SlabPerformanceItem>();

        var statusSummary = await query
            .GroupBy(x => x.PaymentStatus ?? "Pending")
            .Select(g => new StatusSummaryItem
            {
                Status = g.Key,
                TotalNet = g.Sum(x => x.NetTransferAmount),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Fetch lookup options dynamically
        var dbDealerTypes = await db.Parties.Where(p => !p.IsDeleted && p.DealerType != null && p.DealerType != "").Select(p => p.DealerType).Distinct().ToListAsync(cancellationToken);
        var dbAggTypes = await db.Raws.Where(x => !x.IsDeleted && x.ImportLogId > 0 && x.PartyType != null && x.PartyType != "").Select(x => x.PartyType).Distinct().ToListAsync(cancellationToken);
        var dealerTypes = dbDealerTypes.Concat(dbAggTypes).Select(t => NormalizePartyType(t, null)).Distinct().Where(t => !string.IsNullOrEmpty(t)).OrderBy(t => t).ToList();
        var branches = await db.Branches.Where(b => !b.IsDeleted).Select(b => new BranchLookupItem(b.Id, b.Code, b.Name, b.Consignee)).ToListAsync(cancellationToken);
        
        var partiesQuery = db.Parties.Where(p => !p.IsDeleted);
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            partiesQuery = partiesQuery.Where(p => p.BranchId == currentUser.BranchId.Value);
        }
        var parties = await partiesQuery.OrderBy(p => p.PartyName).Select(p => new PartyLookupItem(p.Id, p.PartyCode, p.PartyName, p.OriginalPartyCode)).ToListAsync(cancellationToken);

        var availablePeriods = await db.SsIncentives
            .Where(x => !x.IsDeleted)
            .GroupBy(x => new { Month = x.Month, Year = x.Year })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new { Month = g.Key.Month, Year = g.Key.Year })
            .ToListAsync(cancellationToken);

        var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        var periodsList = availablePeriods.Select(p => (
            p.Month,
            p.Year,
            p.Month >= 1 && p.Month <= 12 ? $"{monthNames[p.Month - 1]} {p.Year}" : $"FY {p.Year}"
        )).ToList();

        var categoriesList = await db.Raws
            .Where(x => !x.IsDeleted && x.ImportLogId > 0 && x.PartCategoryCode != null && x.PartCategoryCode != "")
            .Select(x => x.PartCategoryCode!)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        if (categoriesList.Count == 0)
        {
            categoriesList = new List<string> { "AA", "M", "T" };
        }

        // Dynamically build the RAW sales dataset for Chart.js
        var rawsQuery = db.Raws.Where(x => !x.IsDeleted && x.ImportLogId > 0
            && ((x.YearNumber == targetYear && x.MonthNumber >= 4) || (x.YearNumber == targetYear + 1 && x.MonthNumber <= 3)));

        int lyAnchorYear = targetYear - 1;
        var rawsLYQuery = db.Raws.Where(x => !x.IsDeleted && x.ImportLogId > 0
            && ((x.YearNumber == lyAnchorYear && x.MonthNumber >= 4) || (x.YearNumber == lyAnchorYear + 1 && x.MonthNumber <= 3)));

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var branch = await db.Branches.FindAsync(currentUser.BranchId.Value);
            if (branch != null)
            {
                rawsQuery = rawsQuery.Where(x => x.Loc == branch.Code);
                rawsLYQuery = rawsLYQuery.Where(x => x.Loc == branch.Code);
            }
        }

        var aggs = await rawsQuery
            .GroupBy(x => new {
                Month_Year = x.MonthYear ?? "",
                Quarter = x.Quarter ?? "",
                Party_Type = x.PartyType ?? "",
                Part_Category_Code = x.PartCategoryCode ?? "",
                Loc = x.Loc ?? "",
                Dealer_Sub_Type = x.DealerSubType ?? ""
            })
            .Select(g => new {
                g.Key.Month_Year,
                g.Key.Quarter,
                g.Key.Party_Type,
                g.Key.Part_Category_Code,
                g.Key.Loc,
                g.Key.Dealer_Sub_Type,
                Net_Sales = g.Sum(x => x.NetRetailSelling),
                Net_DDL = g.Sum(x => x.NetRetailDdl),
                Discount = g.Sum(x => x.DiscountAmount),
                Transactions = g.Count()
            })
            .ToListAsync(cancellationToken);

        var aggsLY = await rawsLYQuery
            .GroupBy(x => new {
                Month_Year = x.MonthYear ?? "",
                Quarter = x.Quarter ?? "",
                Party_Type = x.PartyType ?? "",
                Part_Category_Code = x.PartCategoryCode ?? "",
                Loc = x.Loc ?? "",
                Dealer_Sub_Type = x.DealerSubType ?? ""
            })
            .Select(g => new {
                g.Key.Month_Year,
                g.Key.Quarter,
                g.Key.Party_Type,
                g.Key.Part_Category_Code,
                g.Key.Loc,
                g.Key.Dealer_Sub_Type,
                Net_Sales = g.Sum(x => x.NetRetailSelling),
                Net_DDL = g.Sum(x => x.NetRetailDdl),
                Discount = g.Sum(x => x.DiscountAmount),
                Transactions = g.Count()
            })
            .ToListAsync(cancellationToken);

        var consigneeMap = await db.Branches
            .Where(b => !b.IsDeleted)
            .Select(b => new { b.Code, b.Consignee })
            .ToDictionaryAsync(b => b.Code, b => b.Consignee ?? "-", StringComparer.OrdinalIgnoreCase, cancellationToken);

        var rawSalesList = aggs.Select(x => new {
            Month_Year = x.Month_Year,
            Quarter = x.Quarter,
            Party_Type = x.Party_Type,
            Part_Category_Code = x.Part_Category_Code,
            Loc = x.Loc,
            Consignee = consigneeMap.TryGetValue(x.Loc, out var cons) ? cons : "-",
            Dealer_Sub_Type = x.Dealer_Sub_Type,
            Net_Sales = (double)x.Net_Sales,
            Net_DDL = (double)x.Net_DDL,
            Discount = (double)x.Discount,
            Transactions = x.Transactions
        }).ToList();

        var rawSalesListLY = aggsLY.Select(x => new {
            Month_Year = x.Month_Year,
            Quarter = x.Quarter,
            Party_Type = x.Party_Type,
            Part_Category_Code = x.Part_Category_Code,
            Loc = x.Loc,
            Consignee = consigneeMap.TryGetValue(x.Loc, out var cons) ? cons : "-",
            Dealer_Sub_Type = x.Dealer_Sub_Type,
            Net_Sales = (double)x.Net_Sales,
            Net_DDL = (double)x.Net_DDL,
            Discount = (double)x.Discount,
            Transactions = x.Transactions
        }).ToList();

        var serializedRawSales = System.Text.Json.JsonSerializer.Serialize(rawSalesList);
        var serializedRawSalesLY = System.Text.Json.JsonSerializer.Serialize(rawSalesListLY);

        var model = new PerformanceReportsViewModel
        {
            YearSummary = yearSummary,
            MonthSummary = monthSummary,
            EmployeeHistory = employeeHistory,
            DealerHistory = dealerHistory,
            SlabPerformance = slabPerformance,
            StatusSummary = statusSummary,
            RawSalesJson = serializedRawSales,
            RawSalesJsonLastYear = serializedRawSalesLY,
            DealerSalesJson = "[]",

            SelectedDealerTypes = dealerType ?? new(),
            SelectedBranchIds = branchId ?? new(),
            SelectedPartyCodes = partyCode ?? new(),
            SelectedCategories = categories ?? new(),
            SelectedMonth = targetMonth,
            SelectedYear = targetYear,

            AnchorMonth = targetMonth,
            AnchorYear = targetYear,
            CmSales = cmSales,
            CmGross = cmGross,
            CmNet = cmNet,
            CmCount = cmCount,

            SalesMoM = salesMoM,
            IncentiveMoM = incentiveMoM,
            SalesYoY = salesYoY,
            IncentiveYoY = incentiveYoY,

            AvailableDealerTypes = dealerTypes,
            AvailableBranches = branches,
            AvailableParties = parties,
            AvailableCategories = categoriesList,
            AvailablePeriods = periodsList
        };

        return model;
    }

    public async Task<object> GetDealerSalesAsync(
        int targetYear,
        string? quarters,
        string? months,
        string? partyTypes,
        string? categories,
        string? locations,
        string? dealerSubTypes,
        string? search,
        string? limit,
        CancellationToken cancellationToken)
    {
        var query = db.Raws.Where(x => !x.IsDeleted && x.ImportLogId > 0);

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            query = query.Where(x => db.Parties.Any(p => p.PartyCode == x.OriginalCode && p.BranchId == currentUser.BranchId.Value));
        }

        if (!string.IsNullOrEmpty(quarters))
        {
            var qList = quarters.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(q => q.Trim()).ToList();
            query = query.Where(x => x.Quarter != null && qList.Contains(x.Quarter));
        }

        if (!string.IsNullOrEmpty(months))
        {
            var mList = months.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(m => m.Trim()).ToList();
            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var mNums = mList.Select(m => Array.IndexOf(monthNames, m) + 1).Where(n => n > 0).ToList();
            if (mNums.Count > 0)
            {
                query = query.Where(x => x.MonthNumber.HasValue && mNums.Contains(x.MonthNumber.Value));
            }
        }

        if (!string.IsNullOrEmpty(partyTypes))
        {
            var pList = partyTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim().ToUpper()).ToList();
            query = query.Where(x => x.PartyType != null && pList.Contains(x.PartyType.ToUpper()));
        }

        if (!string.IsNullOrEmpty(categories))
        {
            var cList = categories.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToUpper()).ToList();
            query = query.Where(x => x.PartCategoryCode != null && cList.Contains(x.PartCategoryCode.ToUpper()));
        }

        if (!string.IsNullOrEmpty(locations))
        {
            var lList = locations.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim().ToUpper()).ToList();
            query = query.Where(x => x.Loc != null && lList.Contains(x.Loc.ToUpper()));
        }

        if (!string.IsNullOrEmpty(dealerSubTypes))
        {
            var dList = dealerSubTypes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim().ToUpper()).ToList();
            query = query.Where(x => x.DealerSubType != null && dList.Contains(x.DealerSubType.ToUpper()));
        }

        int compYear = targetYear - 1;
        var salesQuery = query.Where(x => 
            ((x.YearNumber == targetYear && x.MonthNumber >= 4) || (x.YearNumber == targetYear + 1 && x.MonthNumber <= 3)) ||
            ((x.YearNumber == compYear && x.MonthNumber >= 4) || (x.YearNumber == compYear + 1 && x.MonthNumber <= 3))
        );

        var groupedSales = await (from raw in salesQuery
                                  group raw by new {
                                      raw.YearNumber,
                                      raw.MonthNumber,
                                      PartyCode = (raw.OriginalCode != null && raw.OriginalCode != "") ? raw.OriginalCode : raw.ConsPartyCode,
                                      PartyName = raw.ConsPartyName,
                                      raw.DealerSubType
                                  } into g
                                  select new {
                                      g.Key.YearNumber,
                                      g.Key.MonthNumber,
                                      g.Key.PartyCode,
                                      g.Key.PartyName,
                                      g.Key.DealerSubType,
                                      Sales = (double)g.Sum(x => x.NetRetailSelling)
                                  })
                                  .ToListAsync(cancellationToken);

        var resultList = groupedSales
            .GroupBy(x => x.PartyCode)
            .Select(g => {
                var first = g.First();
                var activeSales = g.Where(x => 
                    ((x.YearNumber == targetYear && x.MonthNumber >= 4) || (x.YearNumber == targetYear + 1 && x.MonthNumber <= 3))
                ).Sum(x => x.Sales);

                var compSales = g.Where(x => 
                    ((x.YearNumber == compYear && x.MonthNumber >= 4) || (x.YearNumber == compYear + 1 && x.MonthNumber <= 3))
                ).Sum(x => x.Sales);

                var diff = activeSales - compSales;
                var pctVal = compSales > 0 ? (diff / compSales * 100.0) : (double?)null;
                return new {
                    PartyCode = g.Key,
                    PartyName = first.PartyName ?? "-",
                    DealerSubType = first.DealerSubType ?? "AW",
                    ActiveSales = activeSales,
                    CompSales = compSales,
                    Diff = diff,
                    Pct = pctVal
                };
            })
            .OrderByDescending(x => x.ActiveSales)
            .ToList();

        if (!string.IsNullOrEmpty(search))
        {
            var searchUpper = search.ToUpper().Trim();
            resultList = resultList.Where(x => 
                (x.PartyCode != null && x.PartyCode.ToUpper().Contains(searchUpper)) || 
                (x.PartyName != null && x.PartyName.ToUpper().Contains(searchUpper))
            ).ToList();
        }

        int totalCount = resultList.Count;
        int activeCount = resultList.Count(x => x.ActiveSales > 0);
        int compCount = resultList.Count(x => x.CompSales > 0);

        if (!string.IsNullOrEmpty(limit) && limit != "all")
        {
            if (int.TryParse(limit, out int limitNum))
            {
                resultList = resultList.Take(limitNum).ToList();
            }
        }

        return new {
            total = totalCount,
            activeCount = activeCount,
            compCount = compCount,
            rows = resultList
        };
    }

    private string NormalizePartyType(string? dbDealerType, string? partyNameOrCode)
    {
        if (!string.IsNullOrEmpty(dbDealerType))
        {
            return dbDealerType.Trim().ToUpperInvariant();
        }

        if (!string.IsNullOrEmpty(partyNameOrCode) &&
            (partyNameOrCode.Contains("WALK-IN", StringComparison.OrdinalIgnoreCase) ||
             partyNameOrCode.Contains("WALK IN", StringComparison.OrdinalIgnoreCase) ||
             partyNameOrCode.Contains("WALK_IN", StringComparison.OrdinalIgnoreCase) ||
             partyNameOrCode.Contains("WIC", StringComparison.OrdinalIgnoreCase) ||
             partyNameOrCode.Contains("WALKIN", StringComparison.OrdinalIgnoreCase)))
        {
            return "WALK-IN CUSTOMER";
        }

        return "INDEPENDENT WORKSHOP";
    }

    public async Task<TargetVsAchievementViewModel> GetTargetVsAchievementAsync(int? month, int? year, string? branchName, string? partyType, string? partCategoryCode, CancellationToken cancellationToken)
    {
        int targetMonth = month ?? DateTime.UtcNow.Month;
        int targetYear = year ?? DateTime.UtcNow.Year;

        string? branchCode = null;
        if (!string.IsNullOrEmpty(branchName))
        {
            branchCode = await db.Branches.AsNoTracking()
                .Where(b => b.Name == branchName)
                .Select(b => b.Code)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 1. Fetch all unique party codes from the Raw table for the selected period
        var rawPartyCodesQuery = db.Raws.AsNoTracking()
            .Where(r => r.MonthNumber == targetMonth && r.YearNumber == targetYear && !r.IsDeleted);

        if (!string.IsNullOrEmpty(branchCode))
        {
            rawPartyCodesQuery = rawPartyCodesQuery.Where(r => r.Loc == branchCode);
        }

        if (!string.IsNullOrEmpty(partCategoryCode))
        {
            rawPartyCodesQuery = rawPartyCodesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
        }

        var rawPartyCodes = await rawPartyCodesQuery
            .Select(r => r.OriginalCode ?? r.ConsPartyCode)
            .Where(c => c != null && c != "")
            .Distinct()
            .ToListAsync(cancellationToken);

        List<Party> parties;
        if (rawPartyCodes.Any())
        {
            var partiesQuery = db.Parties.AsNoTracking().Include(p => p.Branch)
                .Where(p => !p.IsDeleted && rawPartyCodes.Contains(p.PartyCode)).AsQueryable();

            // Enforce user branch permission
            if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                partiesQuery = partiesQuery.Where(p => p.BranchId == currentUser.BranchId.Value);
            }

            if (!string.IsNullOrEmpty(branchName))
            {
                partiesQuery = partiesQuery.Where(p => p.Branch.Name == branchName);
            }

            if (!string.IsNullOrEmpty(partyType))
            {
                partiesQuery = partiesQuery.Where(p => p.DealerType == partyType);
            }

            parties = await partiesQuery.ToListAsync(cancellationToken);

            // Add virtual placeholder parties for codes in Raw but not in Parties master
            var foundCodes = new HashSet<string>(parties.Select(p => p.PartyCode), StringComparer.OrdinalIgnoreCase);
            var missingCodes = rawPartyCodes.Where(c => !foundCodes.Contains(c)).ToList();
            if (missingCodes.Any())
            {
                var missingRawQuery = db.Raws.AsNoTracking()
                    .Where(r => r.MonthNumber == targetMonth && r.YearNumber == targetYear && !r.IsDeleted && missingCodes.Contains(r.OriginalCode ?? r.ConsPartyCode));

                if (!string.IsNullOrEmpty(branchCode))
                {
                    missingRawQuery = missingRawQuery.Where(r => r.Loc == branchCode);
                }

                var missingRawDetails = await missingRawQuery
                    .Select(r => new { Code = r.OriginalCode ?? r.ConsPartyCode, Name = r.ConsPartyName, BranchCode = r.Loc })
                    .ToListAsync(cancellationToken);

                var missingDict = missingRawDetails
                    .GroupBy(x => x.Code)
                    .ToDictionary(g => g.Key!, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var code in missingCodes)
                {
                    if (missingDict.TryGetValue(code, out var detail))
                    {
                        var normType = NormalizePartyType(null, detail.Name ?? code);
                        if (!string.IsNullOrEmpty(partyType) && !string.Equals(normType, partyType, StringComparison.OrdinalIgnoreCase))
                        {
                            continue; // skip if doesn't match filtered party type
                        }

                        parties.Add(new Party
                        {
                            PartyCode = code,
                            PartyName = detail.Name ?? "Unknown Dealer",
                            Branch = new Branch { Name = detail.BranchCode ?? "Head Office" },
                            DealerType = normType
                        });
                    }
                }
            }
        }
        else
        {
            var partiesQuery = db.Parties.AsNoTracking().Include(p => p.Branch).Where(p => !p.IsDeleted).AsQueryable();

            // Enforce user branch permission
            if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
            {
                partiesQuery = partiesQuery.Where(p => p.BranchId == currentUser.BranchId.Value);
            }

            if (!string.IsNullOrEmpty(branchName))
            {
                partiesQuery = partiesQuery.Where(p => p.Branch.Name == branchName);
            }

            if (!string.IsNullOrEmpty(partyType))
            {
                partiesQuery = partiesQuery.Where(p => p.DealerType == partyType);
            }

            parties = await partiesQuery.ToListAsync(cancellationToken);
        }

        var allBranches = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).Select(b => b.Name).OrderBy(n => n).ToListAsync(cancellationToken);
        var allPartyTypes = await db.Parties.AsNoTracking()
            .Where(p => !p.IsDeleted && p.DealerType != "")
            .Select(p => p.DealerType)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(cancellationToken);
        var partyCodes = parties.Select(p => p.PartyCode).ToList();

        var targets = await db.DealerTargets.AsNoTracking()
            .Where(t => t.Month == targetMonth && t.Year == targetYear && !t.IsDeleted && partyCodes.Contains(t.PartyCode))
            .ToDictionaryAsync(t => t.PartyCode, t => t, StringComparer.OrdinalIgnoreCase, cancellationToken);

        int lmMonth = targetMonth - 1;
        int lmYear = targetYear;
        if (lmMonth == 0) { lmMonth = 12; lmYear--; }

        int fyStartYear = targetMonth >= 4 ? targetYear : targetYear - 1;
        int lyFyStartYear = fyStartYear - 1;
        int lyTargetYear = targetYear - 1;

        // Current Month Sales from Raw Query
        var currentSalesQuery = db.Raws.AsNoTracking()
            .Where(r => r.MonthNumber == targetMonth && r.YearNumber == targetYear && !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)));

        // Last Month Sales from Raw Query
        var lastMonthSalesQuery = db.Raws.AsNoTracking()
            .Where(r => r.MonthNumber == lmMonth && r.YearNumber == lmYear && !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)));

        // YTD Sales (Current Year) from Raw Query
        var ytdSalesQuery = db.Raws.AsNoTracking()
            .Where(r => !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)) &&
                       ((r.YearNumber == fyStartYear && r.MonthNumber >= 4) || (r.YearNumber == fyStartYear + 1 && r.MonthNumber < 4)) &&
                       (r.YearNumber < targetYear || (r.YearNumber == targetYear && r.MonthNumber <= targetMonth)));

        // YTD Sales (Last Year) from Raw Query
        var lyYtdSalesQuery = db.Raws.AsNoTracking()
            .Where(r => !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)) &&
                       ((r.YearNumber == lyFyStartYear && r.MonthNumber >= 4) || (r.YearNumber == lyFyStartYear + 1 && r.MonthNumber < 4)) &&
                       (r.YearNumber < lyTargetYear || (r.YearNumber == lyTargetYear && r.MonthNumber <= targetMonth)));

        // Fetch historical prior year sales in same month Query
        var priorSalesQuery = db.Raws.AsNoTracking()
            .Where(r => r.MonthNumber == targetMonth && r.YearNumber < targetYear && !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)));

        // Construct 6 prior months periods
        var last6MonthsPeriods = new List<(int Month, int Year)>();
        int curM = targetMonth;
        int curY = targetYear;
        for (int i = 0; i < 6; i++)
        {
            curM--;
            if (curM == 0) { curM = 12; curY--; }
            last6MonthsPeriods.Add((curM, curY));
        }

        // Fetch sales for all partyCodes in those 6 periods Query
        var last6MonthsSalesQuery = db.Raws.AsNoTracking()
            .Where(r => !r.IsDeleted && (partyCodes.Contains(r.OriginalCode) || partyCodes.Contains(r.ConsPartyCode)) &&
                       ((r.YearNumber == last6MonthsPeriods[0].Year && r.MonthNumber == last6MonthsPeriods[0].Month) ||
                        (r.YearNumber == last6MonthsPeriods[1].Year && r.MonthNumber == last6MonthsPeriods[1].Month) ||
                        (r.YearNumber == last6MonthsPeriods[2].Year && r.MonthNumber == last6MonthsPeriods[2].Month) ||
                        (r.YearNumber == last6MonthsPeriods[3].Year && r.MonthNumber == last6MonthsPeriods[3].Month) ||
                        (r.YearNumber == last6MonthsPeriods[4].Year && r.MonthNumber == last6MonthsPeriods[4].Month) ||
                        (r.YearNumber == last6MonthsPeriods[5].Year && r.MonthNumber == last6MonthsPeriods[5].Month)));

        if (!string.IsNullOrEmpty(partCategoryCode))
        {
            currentSalesQuery = currentSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
            lastMonthSalesQuery = lastMonthSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
            ytdSalesQuery = ytdSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
            lyYtdSalesQuery = lyYtdSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
            priorSalesQuery = priorSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
            last6MonthsSalesQuery = last6MonthsSalesQuery.Where(r => r.PartCategoryCode == partCategoryCode);
        }

        var currentSalesList = await currentSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var currentSalesDict = currentSalesList
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Sum(x => x.NetRetailSelling), StringComparer.OrdinalIgnoreCase);

        var lastMonthSalesList = await lastMonthSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var lastMonthSalesDict = lastMonthSalesList
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Sum(x => x.NetRetailSelling), StringComparer.OrdinalIgnoreCase);

        var ytdSalesList = await ytdSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var ytdSalesDict = ytdSalesList
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Sum(x => x.NetRetailSelling), StringComparer.OrdinalIgnoreCase);

        var lyYtdSalesList = await lyYtdSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var lyYtdSalesDict = lyYtdSalesList
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Sum(x => x.NetRetailSelling), StringComparer.OrdinalIgnoreCase);

        var scheme = await db.IncentiveSchemes.AsNoTracking()
            .Include(s => s.Details)
            .FirstOrDefaultAsync(s => s.SchemeMonth == targetMonth && s.SchemeYear == targetYear && !s.IsDeleted, cancellationToken);

        if (scheme == null)
        {
            scheme = await db.IncentiveSchemes.AsNoTracking()
                .Include(s => s.Details)
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.SchemeYear)
                .ThenByDescending(s => s.SchemeMonth)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var priorSalesList = await priorSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.YearNumber, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var priorSalesDict = priorSalesList
            .GroupBy(x => new { x.PartyCode, x.YearNumber })
            .Select(g => new { g.Key.PartyCode, Sales = g.Sum(x => x.NetRetailSelling) })
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Select(x => x.Sales).ToList(), StringComparer.OrdinalIgnoreCase);

        var last6MonthsSalesList = await last6MonthsSalesQuery
            .Select(r => new { PartyCode = r.OriginalCode ?? r.ConsPartyCode, r.YearNumber, r.MonthNumber, r.NetRetailSelling })
            .ToListAsync(cancellationToken);

        var last6SalesDict = last6MonthsSalesList
            .GroupBy(x => new { x.PartyCode, x.YearNumber, x.MonthNumber })
            .Select(g => new { g.Key.PartyCode, Sales = g.Sum(x => x.NetRetailSelling) })
            .GroupBy(x => x.PartyCode)
            .ToDictionary(g => g.Key!, g => g.Select(x => x.Sales).ToList(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<TargetVsAchievementRow>();
        foreach (var p in parties)
        {
            targets.TryGetValue(p.PartyCode, out var tgt);
            currentSalesDict.TryGetValue(p.PartyCode, out var currentSales);
            lastMonthSalesDict.TryGetValue(p.PartyCode, out var lmSales);
            ytdSalesDict.TryGetValue(p.PartyCode, out var ytd);
            lyYtdSalesDict.TryGetValue(p.PartyCode, out var lyYtd);

            priorSalesDict.TryGetValue(p.PartyCode, out var priorHistory);
            last6SalesDict.TryGetValue(p.PartyCode, out var recentHistory);

            var history = new List<decimal>();
            if (priorHistory != null) history.AddRange(priorHistory);
            if (recentHistory != null) history.AddRange(recentHistory);
            history = history.Where(h => h > 0m).OrderBy(h => h).ToList();

            decimal calculatedMedian = 0m;
            if (history.Any())
            {
                int count = history.Count;
                if (count % 2 == 0)
                {
                    calculatedMedian = (history[count / 2 - 1] + history[count / 2]) / 2m;
                }
                else
                {
                    calculatedMedian = history[count / 2];
                }
            }
            else
            {
                calculatedMedian = lmSales > 0 ? lmSales : 0m;
            }

            decimal suggestedTgt = tgt?.SystemSuggestedTarget ?? Math.Round(calculatedMedian * 1.1m, 2);
            decimal? adminTgt = tgt?.AdminDefinedTarget;

            // Round targets to the nearest thousand
            Func<decimal, decimal> roundToThousand = (val) => Math.Round(val / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m;
            suggestedTgt = roundToThousand(suggestedTgt);
            if (adminTgt.HasValue)
            {
                adminTgt = roundToThousand(adminTgt.Value);
            }
            decimal finalTgt = adminTgt ?? suggestedTgt;

            decimal targetAchievementPercent = finalTgt > 0 ? (currentSales / finalTgt) * 100m : 0m;

            string slabLabel = "None";
            decimal nextSlabTarget = 0m;
            if (scheme != null && scheme.Details.Any())
            {
                var sortedDetails = scheme.Details.OrderBy(d => d.MinAchievementPercent).ToList();
                var currentSlab = sortedDetails.FirstOrDefault(d => targetAchievementPercent >= d.MinAchievementPercent && targetAchievementPercent <= d.MaxAchievementPercent);
                if (currentSlab != null)
                {
                    slabLabel = currentSlab.RuleName + $" ({currentSlab.Percentage}%)";
                    var nextSlab = sortedDetails.FirstOrDefault(d => d.MinAchievementPercent > targetAchievementPercent);
                    if (nextSlab != null)
                    {
                        nextSlabTarget = (nextSlab.MinAchievementPercent / 100m) * finalTgt;
                    }
                }
                else
                {
                    var firstSlab = sortedDetails.FirstOrDefault();
                    if (firstSlab != null)
                    {
                        nextSlabTarget = (firstSlab.MinAchievementPercent / 100m) * finalTgt;
                    }
                }
            }

            decimal growthPercent = lyYtd > 0 ? ((ytd - lyYtd) / lyYtd) * 100m : 0m;

            rows.Add(new TargetVsAchievementRow
            {
                PartyCode = p.PartyCode,
                PartyName = p.PartyName,
                BranchName = p.Branch?.Name ?? "Head Office",
                SystemSuggestedTarget = suggestedTgt,
                AdminDefinedTarget = adminTgt,
                FinalTarget = finalTgt,
                CurrentAchievementSales = Math.Round(currentSales, 0),
                AchievementPercent = Math.Round(targetAchievementPercent, 1),
                CurrentSlabLabel = slabLabel,
                NextSlabTarget = Math.Round(nextSlabTarget, 0),
                LastMonthSales = Math.Round(lmSales, 0),
                YTDSales = Math.Round(ytd, 0),
                LastYearYTDSales = Math.Round(lyYtd, 0),
                YoYGrowthPercent = Math.Round(growthPercent, 1)
            });
        }

        var allPartCategories = await db.Raws.AsNoTracking()
            .Where(r => !r.IsDeleted && r.PartCategoryCode != null && r.PartCategoryCode != "")
            .Select(r => r.PartCategoryCode)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        return new TargetVsAchievementViewModel
        {
            SelectedMonth = targetMonth,
            SelectedYear = targetYear,
            SelectedBranch = branchName,
            AvailableBranches = allBranches,
            SelectedPartyType = partyType,
            AvailablePartyTypes = allPartyTypes,
            SelectedPartCategory = partCategoryCode,
            AvailablePartCategories = allPartCategories ?? new List<string>(),
            Rows = rows
        };
    }
}

