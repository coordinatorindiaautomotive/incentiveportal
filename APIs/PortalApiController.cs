using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.APIs;

[ApiController]
[Authorize(AuthenticationSchemes = $"{CookieAuthenticationDefaults.AuthenticationScheme},{JwtBearerDefaults.AuthenticationScheme}")]
[Route("api")]
[IgnoreAntiforgeryToken]
public sealed class PortalApiController(IDashboardService dashboardService, IPartyService partyService, IncentiveDbContext db, ICurrentUser currentUser, ISalesImportService importService) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<DashboardSummary> Dashboard(CancellationToken cancellationToken) => dashboardService.GetSummaryAsync(cancellationToken);

    [HttpGet("parties")]
    public async Task<IReadOnlyList<PartyDto>> Parties(CancellationToken cancellationToken)
        => await partyService.QueryForCurrentUser()
            .Select(x => new PartyDto(x.Id, x.PartyCode, x.PartyName, x.GST, x.Mobile, x.Address, x.BranchId, x.Branch.Name, x.DealerType, x.Status, x.FixedIncentivePercent))
            .ToListAsync(cancellationToken);

    [HttpGet("branch/{branchId}/analytics")]
    public async Task<BranchAnalyticsDto> BranchAnalytics(int branchId, CancellationToken cancellationToken)
    {
        if (!currentUser.CanAccessBranch(branchId))
            throw new UnauthorizedAccessException("Branch access denied.");

        var branchPartyCodes = await db.Parties
            .AsNoTracking()
            .Where(x => x.BranchId == branchId)
            .Select(x => x.PartyCode)
            .ToListAsync(cancellationToken);

        var incentives = await db.SsIncentives
            .AsNoTracking()
            .Where(x => branchPartyCodes.Contains(x.PartyCode) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var latestPeriod = incentives
            .Select(x => new { x.Year, x.Month })
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .FirstOrDefault();

        var currentYear = latestPeriod?.Year ?? DateTime.UtcNow.Year;
        var currentMonth = latestPeriod?.Month ?? DateTime.UtcNow.Month;
        var previousMonth = currentMonth == 1 ? 12 : currentMonth - 1;
        var previousYear = currentMonth == 1 ? currentYear - 1 : currentYear;

        var currentSales = incentives.Where(x => x.Year == currentYear && x.Month == currentMonth).ToList();
        var previousSales = incentives.Where(x => x.Year == previousYear && x.Month == previousMonth).ToList();

        static decimal Growth(decimal current, decimal previous)
            => previous <= 0 ? (current <= 0 ? 0 : 100) : Math.Round((current - previous) / previous * 100, 2);

        var totalSales = currentSales.Sum(x => x.SaleValue);
        var totalIncentive = currentSales.Sum(x => x.GrossIncentive);
        var priorSales = previousSales.Sum(x => x.SaleValue);

        var partyGrowth = currentSales
            .Select(x =>
            {
                var salesValue = x.SaleValue;
                var incentive = x.GrossIncentive;
                var previousSalesValue = previousSales.Where(y => y.PartyCode == x.PartyCode).Sum(y => y.SaleValue);
                var growthMoM = Growth(salesValue, previousSalesValue);
                return new PartyPerformanceDto(x.PartyCode, x.PartyName, salesValue, incentive, x.SlabPercent * 100m, growthMoM, 0, 0);
            })
            .OrderByDescending(x => x.Sales)
            .ToList();

        var topDealers = partyGrowth.Take(5).ToList();
        var weakDealers = partyGrowth.OrderBy(x => x.GrowthMoM).Take(5).ToList();

        // Slab achievement calculation
        decimal slabAchievement = currentSales.Count > 0
            ? Math.Round((decimal)currentSales.Count(x => x.SlabPercent > 0) / currentSales.Count * 100m, 2)
            : 0m;

        // Branch-level insights
        var smartInsights = new List<SmartInsightDto>();
        foreach (var dealer in weakDealers.Where(x => x.GrowthMoM < -15m))
        {
            smartInsights.Add(new SmartInsightDto("Decline", $"Declining: {dealer.PartyName} purchases are down {Math.Abs(dealer.GrowthMoM):0.##}% MoM.", "High"));
        }
        if (slabAchievement < 60m)
        {
            smartInsights.Add(new SmartInsightDto("Performance", $"Low Slab Achievement: Only {slabAchievement}% of branch dealers reached any incentive slab.", "High"));
        }
        else
        {
            smartInsights.Add(new SmartInsightDto("Stability", "Dealers in this branch are exhibiting consistent sales achievement.", "Low"));
        }

        var branchName = await db.Branches.Where(x => x.Id == branchId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken) ?? "Branch";

        return new BranchAnalyticsDto(
            branchName,
            totalSales,
            totalIncentive,
            Growth(totalSales, priorSales),
            topDealers,
            weakDealers,
            slabAchievement,
            smartInsights);
    }

    [HttpGet("party/{partyId}/summary")]
    public async Task<PartySummaryDto> PartySummary(int partyId, CancellationToken cancellationToken)
    {
        var party = await db.Parties.Include(x => x.Branch).FirstOrDefaultAsync(x => x.Id == partyId, cancellationToken);
        if (party is null)
            throw new InvalidOperationException("Party not found.");
        if (!currentUser.CanAccessBranch(party.BranchId))
            throw new UnauthorizedAccessException("Party access denied.");

        var allSales = await db.SsIncentives.Where(x => x.PartyCode == party.PartyCode && !x.IsDeleted).ToListAsync(cancellationToken);
        var latestSale = allSales.OrderByDescending(x => x.Year).ThenByDescending(x => x.Month).FirstOrDefault();
        if (latestSale is null)
            return new PartySummaryDto(party.Id, party.PartyCode, party.PartyName, 0, 0, 0, 0, 0, 0, 0, 0, 0, new List<IncentiveSchemeSlabDto>());

        var previousSale = allSales.Where(x => x.Year == latestSale.Year && x.Month == latestSale.Month - 1).FirstOrDefault();
        var lastYearSale = allSales.Where(x => x.Year == latestSale.Year - 1 && x.Month == latestSale.Month).FirstOrDefault();
        var currentIncentive = latestSale.GrossIncentive;

        static decimal Growth(decimal current, decimal previous)
            => previous <= 0 ? (current <= 0 ? 0 : 100) : Math.Round((current - previous) / previous * 100, 2);

        var isFixedIncentive = party.DealerType == "Fixed Incentive";
        decimal growthMoM = Growth(latestSale.SaleValue, previousSale?.SaleValue ?? 0);
        decimal growthYoY = Growth(latestSale.SaleValue, lastYearSale?.SaleValue ?? 0);
        var nextSlabPercent = 0m;
        var additionalPurchaseRequired = 0m;
        var nextIncentive = 0m;
        var progressPercent = 100m;

        var activeSlabs = new List<IncentiveSchemeSlabDto>();
        if (isFixedIncentive)
        {
            activeSlabs.Add(new IncentiveSchemeSlabDto(0, 100, party.FixedIncentivePercent, 0, "Fixed Payout Rate"));
            nextIncentive = currentIncentive;
        }
        else
        {
            var scheme = await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => x.SchemeMonth == latestSale.Month && x.SchemeYear == latestSale.Year)
                .OrderByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? await db.IncentiveSchemes.Include(x => x.Details)
                    .Where(x => x.SchemeYear < latestSale.Year || (x.SchemeYear == latestSale.Year && x.SchemeMonth <= latestSale.Month))
                    .OrderByDescending(x => x.SchemeYear)
                    .ThenByDescending(x => x.SchemeMonth)
                    .ThenByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken)
                ?? await db.IncentiveSchemes.Include(x => x.Details)
                    .OrderByDescending(x => x.SchemeYear)
                    .ThenByDescending(x => x.SchemeMonth)
                    .ThenByDescending(x => x.Version)
                    .FirstOrDefaultAsync(cancellationToken);

            activeSlabs = scheme?.Details.Select(x => new IncentiveSchemeSlabDto(x.MinAchievementPercent, x.MaxAchievementPercent, x.Percentage ?? 0m, x.FixedAmount ?? 0m, x.RuleName)).ToList() ?? new List<IncentiveSchemeSlabDto>();

            if (scheme is not null)
            {
                var currentDetail = scheme.Details.FirstOrDefault(x => latestSale.SaleValue >= x.MinAchievementPercent && latestSale.SaleValue <= x.MaxAchievementPercent);
                var nextDetail = currentDetail is null
                    ? scheme.Details.OrderBy(x => x.MinAchievementPercent).FirstOrDefault(x => x.MinAchievementPercent > latestSale.SaleValue)
                    : scheme.Details.OrderBy(x => x.MinAchievementPercent).FirstOrDefault(x => x.MinAchievementPercent > currentDetail.MinAchievementPercent);
                if (nextDetail is not null)
                {
                    nextSlabPercent = nextDetail.Percentage ?? 0m;
                    var rawTarget = nextDetail.MinAchievementPercent;
                    additionalPurchaseRequired = Math.Round(Math.Max(0m, rawTarget - latestSale.SaleValue), 0, MidpointRounding.AwayFromZero);
                    nextIncentive = Math.Round(Math.Max(0, (nextDetail.FixedAmount ?? 0m) + rawTarget * ((nextDetail.Percentage ?? 0m) / 100m)), 0, MidpointRounding.AwayFromZero);
                    progressPercent = rawTarget > 0 ? Math.Round((latestSale.SaleValue / rawTarget) * 100m, 2) : 100m;
                }
            }
        }

        return new PartySummaryDto(
            party.Id,
            party.PartyCode,
            party.PartyName,
            latestSale.SaleValue,
            isFixedIncentive ? party.FixedIncentivePercent : latestSale.SlabPercent * 100m,
            currentIncentive,
            nextSlabPercent,
            additionalPurchaseRequired,
            nextIncentive,
            growthMoM,
            growthYoY,
            progressPercent,
            activeSlabs);
    }

    [HttpGet("party/{partyId}/history")]
    public async Task<IReadOnlyList<DealerHistoryDto>> PartyHistory(int partyId, CancellationToken cancellationToken)
    {
        var party = await db.Parties.FirstOrDefaultAsync(x => x.Id == partyId, cancellationToken);
        if (party is null)
            throw new InvalidOperationException("Party not found.");
        if (!currentUser.CanAccessBranch(party.BranchId))
            throw new UnauthorizedAccessException("Party access denied.");

        var sales = await db.SsIncentives
            .Where(x => x.PartyCode == party.PartyCode && !x.IsDeleted)
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .Take(12)
            .ToListAsync(cancellationToken);

        var history = sales.Select(s =>
        {
            var gross = s.GrossIncentive;
            var tds = s.TdsAmount;
            var net = s.NetTransferAmount;

            var monthName = new DateTime(s.Year, s.Month, 1).ToString("MMMM yyyy");
            var creditedOn = s.PaymentDate?.ToString("dd-MMM-yyyy") ?? "-";
            var status = s.PaymentStatus;
            var utr = s.UTRNumber ?? "-";

            return new DealerHistoryDto(
                monthName,
                s.Month,
                s.Year,
                s.SaleValue,
                gross,
                s.SlabPercent * 100m,
                tds,
                net,
                status,
                creditedOn,
                utr
            );
        }).ToList();

        return history;
    }

    // =========================================================================
    // 1. POST /api/incentive/upload
    // =========================================================================
    [HttpPost("incentive/upload")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance},{AppRoles.BranchManager}")]
    public async Task<IActionResult> UploadIncentiveFile(IFormFile file, [FromForm] string? uploadMode = null, [FromForm] bool commit = true, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a valid Excel file." });

        try
        {
            var rows = await importService.PreviewAsync(file, uploadMode, null, null, cancellationToken);
            if (rows.Any(r => !string.IsNullOrEmpty(r.Error)))
            {
                return BadRequest(new { message = "Validation errors found in Excel preview.", errors = rows.Where(r => !string.IsNullOrEmpty(r.Error)).Select(r => new { r.RowNumber, r.PartyCode, r.Error }) });
            }

            if (commit)
            {
                var summary = await importService.CommitAsync(rows, file.FileName, cancellationToken: cancellationToken);
                var log = summary.Log;
                return Ok(new
                {
                    ok = summary.Skipped == 0,
                    log.Id,
                    summary.TotalRows,
                    SuccessRows = summary.Committed,
                    FailedRows = summary.Skipped,
                    message = $"Successfully committed and imported {summary.Committed:N0} rows."
                });
            }

            return Ok(new
            {
                ok = true,
                message = "Excel file validation completed successfully with zero errors.",
                previewRowsCount = rows.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================================
    // 2. GET /api/incentive/dealer-summary?party=&month=
    // =========================================================================
    [HttpGet("incentive/dealer-summary")]
    public async Task<IActionResult> GetDealerSummary([FromQuery] string party, [FromQuery] string? month, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(party))
            return BadRequest("Party parameter is required.");

        var partyQuery = db.Parties.Include(x => x.Branch).AsQueryable();
        Party? partyObj;
        if (int.TryParse(party, out var partyId))
            partyObj = await partyQuery.FirstOrDefaultAsync(x => x.Id == partyId, cancellationToken);
        else
            partyObj = await partyQuery.FirstOrDefaultAsync(x => x.PartyCode == party, cancellationToken);

        if (partyObj == null)
            return NotFound("Party not found.");

        if (!currentUser.CanAccessBranch(partyObj.BranchId))
            return Forbid();

        int targetMonth, targetYear;
        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
            {
                targetYear = y;
                targetMonth = m;
            }
            else if (int.TryParse(month, out var mVal))
            {
                targetMonth = mVal;
                targetYear = DateTime.UtcNow.Year;
            }
            else
            {
                return BadRequest("Invalid month format. Expected YYYY-MM or MM.");
            }
        }
        else
        {
            var latest = await db.SsIncentives.Where(x => x.PartyCode == partyObj.PartyCode)
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .FirstOrDefaultAsync(cancellationToken);
            targetMonth = latest?.Month ?? DateTime.UtcNow.Month;
            targetYear = latest?.Year ?? DateTime.UtcNow.Year;
        }

        var summary = await db.IncentiveSummaries.FirstOrDefaultAsync(x => x.PartyId == partyObj.Id && x.Month == targetMonth && x.Year == targetYear, cancellationToken);
        var growth = await db.DealerGrowthAnalytics.FirstOrDefaultAsync(x => x.PartyId == partyObj.Id && x.Month == targetMonth && x.Year == targetYear, cancellationToken);

        decimal currentSale = summary?.CurrentSale ?? 0m;
        decimal slabPercent = (summary?.CurrentSlabPercent ?? 0m) * 100m;
        decimal incentiveEarned = summary?.CurrentIncentive ?? 0m;
        decimal nextSlabTarget = summary?.NextSlabTarget ?? 0m;
        decimal gapToNextSlab = summary?.AdditionalPurchaseRequired ?? 0m;
        decimal projectedIncentiveAtNextSlab = summary?.NextIncentive ?? 0m;
        decimal momDelta = growth?.SalesGrowthMoM ?? 0m;
        decimal yoyDelta = growth?.SalesGrowthYoY ?? 0m;

        var flags = new List<string>();
        if (gapToNextSlab > 0m && nextSlabTarget > 0m && gapToNextSlab <= 0.15m * nextSlabTarget)
            flags.Add("Close to slab");
        if (momDelta < -10m)
            flags.Add("Declining");
        if (yoyDelta > 30m)
            flags.Add("High growth");

        return Ok(new
        {
            partyCode = partyObj.PartyCode,
            partyName = partyObj.PartyName,
            branch = partyObj.Branch.Name,
            month = targetMonth,
            year = targetYear,
            currentSale,
            slabPercent,
            incentiveEarned,
            gapToNextSlab,
            projectedIncentiveAtNextSlab,
            momDelta,
            yoyDelta,
            flags
        });
    }

    // =========================================================================
    // 3. GET /api/incentive/branch-analytics?branch=&month=
    // =========================================================================
    [HttpGet("incentive/branch-analytics")]
    public async Task<IActionResult> GetBranchAnalytics([FromQuery] string branch, [FromQuery] string? month, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(branch))
            return BadRequest("Branch parameter is required.");

        Branch? branchObj;
        if (int.TryParse(branch, out var branchId))
            branchObj = await db.Branches.FirstOrDefaultAsync(x => x.Id == branchId, cancellationToken);
        else
            branchObj = await db.Branches.FirstOrDefaultAsync(x => x.Code == branch, cancellationToken);

        if (branchObj == null)
            return NotFound("Branch not found.");

        if (!currentUser.CanAccessBranch(branchObj.Id))
            return Forbid();

        int targetMonth, targetYear;
        var partyCodes = await db.Parties.Where(x => x.BranchId == branchObj.Id).Select(x => x.PartyCode).ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(month))
        {
            var parts = month.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
            {
                targetYear = y;
                targetMonth = m;
            }
            else if (int.TryParse(month, out var mVal))
            {
                targetMonth = mVal;
                targetYear = DateTime.UtcNow.Year;
            }
            else
            {
                return BadRequest("Invalid month format. Expected YYYY-MM or MM.");
            }
        }
        else
        {
            var latest = await db.SsIncentives.Where(x => partyCodes.Contains(x.PartyCode))
                .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
                .FirstOrDefaultAsync(cancellationToken);
            targetMonth = latest?.Month ?? DateTime.UtcNow.Month;
            targetYear = latest?.Year ?? DateTime.UtcNow.Year;
        }

        var incentives = await db.SsIncentives
            .Where(x => partyCodes.Contains(x.PartyCode) && x.Month == targetMonth && x.Year == targetYear)
            .ToListAsync(cancellationToken);

        var totalSales = incentives.Sum(x => x.SaleValue);
        var totalIncentive = incentives.Sum(x => x.GrossIncentive);

        // Previous month sales for growth
        int prevMonth = targetMonth == 1 ? 12 : targetMonth - 1;
        int prevYear = targetMonth == 1 ? targetYear - 1 : targetYear;
        var prevSalesSum = await db.SsIncentives
            .Where(x => partyCodes.Contains(x.PartyCode) && x.Month == prevMonth && x.Year == prevYear)
            .SumAsync(x => (decimal?)x.SaleValue, cancellationToken) ?? 0m;

        decimal growthPercent = prevSalesSum <= 0m ? (totalSales <= 0m ? 0m : 100m) : Math.Round((totalSales - prevSalesSum) / prevSalesSum * 100m, 2);

        var rankings = incentives.Select(x => new
        {
            partyCode = x.PartyCode,
            partyName = x.PartyName,
            sales = x.SaleValue,
            incentive = x.GrossIncentive,
            slabPercent = x.SlabPercent * 100
        }).OrderByDescending(x => x.sales).ToList();

        return Ok(new
        {
            branchCode = branchObj.Code,
            branchName = branchObj.Name,
            month = targetMonth,
            year = targetYear,
            totalSales,
            growthPercent,
            incentivePayout = totalIncentive,
            rankings
        });
    }

    // =========================================================================
    // 4. GET /api/incentive/slab-progress?party=
    // =========================================================================
    [HttpGet("incentive/slab-progress")]
    public async Task<IActionResult> GetSlabProgress([FromQuery] string party, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(party))
            return BadRequest("Party parameter is required.");

        var partyQuery = db.Parties.AsQueryable();
        Party? partyObj;
        if (int.TryParse(party, out var partyId))
            partyObj = await partyQuery.FirstOrDefaultAsync(x => x.Id == partyId, cancellationToken);
        else
            partyObj = await partyQuery.FirstOrDefaultAsync(x => x.PartyCode == party, cancellationToken);

        if (partyObj == null)
            return NotFound("Party not found.");

        if (!currentUser.CanAccessBranch(partyObj.BranchId))
            return Forbid();

        var latestProg = await db.DealerSlabProgresses
            .Where(x => x.PartyId == partyObj.Id)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestProg == null)
        {
            return Ok(new
            {
                partyCode = partyObj.PartyCode,
                progressPercent = 0m,
                currentSale = 0m,
                nextSlabThreshold = 0m,
                remainingGap = 0m,
                projectedIncentiveAtNextSlab = 0m
            });
        }

        var summary = await db.IncentiveSummaries.FirstOrDefaultAsync(x => x.PartyId == partyObj.Id && x.Month == latestProg.Month && x.Year == latestProg.Year, cancellationToken);

        return Ok(new
        {
            partyCode = partyObj.PartyCode,
            progressPercent = latestProg.ProgressPercent,
            currentSale = latestProg.CurrentSale,
            nextSlabThreshold = latestProg.NextSlabTarget,
            remainingGap = latestProg.RemainingAmount,
            projectedIncentiveAtNextSlab = summary?.NextIncentive ?? 0m
        });
    }

    // =========================================================================
    // 5. GET /api/dashboard/ho
    // =========================================================================
    [HttpGet("dashboard/ho")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance},{AppRoles.Auditor}")]
    public async Task<IActionResult> GetHoDashboard(CancellationToken cancellationToken)
    {
        var summary = await dashboardService.GetSummaryAsync(cancellationToken);
        return Ok(new
        {
            totalIncentivePayoutLiability = summary.TotalIncentive,
            pendingApprovalsCount = summary.PendingApprovals,
            budgetVsActualRatio = summary.BudgetVsActualPercent,
            activePartiesCount = summary.ActiveParties,
            branchRankings = summary.BranchRankings.Select(x => new
            {
                x.BranchName,
                x.Code,
                totalSales = x.TotalSales,
                incentiveGenerated = x.IncentiveGenerated,
                growthPercent = x.GrowthPercent
            }),
            highValueDealers = summary.TopDealers.Select(x => new
            {
                x.PartyCode,
                x.PartyName,
                sales = x.Sales,
                incentive = x.Incentive,
                slabPercent = x.SlabPercent
            }),
            pendingApprovalsQueue = summary.WorkQueue
        });
    }

    // =========================================================================
    // 6. GET /api/dashboard/branch
    // =========================================================================
    [HttpGet("dashboard/branch")]
    public async Task<IActionResult> GetBranchDashboard([FromQuery] string? branch, CancellationToken cancellationToken)
    {
        int resolvedBranchId;
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            resolvedBranchId = currentUser.BranchId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(branch))
        {
            Branch? branchObj;
            if (int.TryParse(branch, out var bId))
                branchObj = await db.Branches.FirstOrDefaultAsync(x => x.Id == bId, cancellationToken);
            else
                branchObj = await db.Branches.FirstOrDefaultAsync(x => x.Code == branch, cancellationToken);

            if (branchObj == null) return NotFound("Branch not found.");
            resolvedBranchId = branchObj.Id;
        }
        else
        {
            var firstBr = await db.Branches.FirstOrDefaultAsync(cancellationToken);
            if (firstBr == null) return NotFound("No branches registered.");
            resolvedBranchId = firstBr.Id;
        }

        var analytics = await BranchAnalytics(resolvedBranchId, cancellationToken);
        return Ok(analytics);
    }

    // =========================================================================
    // 7. GET /api/dashboard/dealer
    // =========================================================================
    [HttpGet("dashboard/dealer")]
    public async Task<IActionResult> GetDealerDashboard([FromQuery] string? party, CancellationToken cancellationToken)
    {
        var partyQuery = db.Parties.AsQueryable();
        Party? partyObj = null;

        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            if (!string.IsNullOrWhiteSpace(party))
            {
                if (int.TryParse(party, out var pId))
                    partyObj = await partyQuery.FirstOrDefaultAsync(x => x.Id == pId && x.BranchId == currentUser.BranchId, cancellationToken);
                else
                    partyObj = await partyQuery.FirstOrDefaultAsync(x => x.PartyCode == party && x.BranchId == currentUser.BranchId, cancellationToken);
            }
            else
            {
                partyObj = await partyQuery.FirstOrDefaultAsync(x => x.BranchId == currentUser.BranchId, cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(party))
        {
            if (int.TryParse(party, out var pId))
                partyObj = await partyQuery.FirstOrDefaultAsync(x => x.Id == pId, cancellationToken);
            else
                partyObj = await partyQuery.FirstOrDefaultAsync(x => x.PartyCode == party, cancellationToken);
        }
        else
        {
            partyObj = await partyQuery.FirstOrDefaultAsync(cancellationToken);
        }

        if (partyObj == null)
            return NotFound("Party or dealer record not resolved.");

        var summary = await PartySummary(partyObj.Id, cancellationToken);
        var history = await PartyHistory(partyObj.Id, cancellationToken);

        return Ok(new
        {
            dealerInfo = new
            {
                partyObj.Id,
                partyObj.PartyCode,
                partyObj.PartyName,
                partyObj.GST,
                partyObj.Mobile,
                partyObj.Address
            },
            currentMonthPerformance = new
            {
                currentPurchase = summary.CurrentSale,
                slabPercent = summary.CurrentSlabPercent,
                incentiveEarned = summary.CurrentIncentive,
                momGrowth = summary.GrowthMoM,
                yoyGrowth = summary.GrowthYoY
            },
            slabProgress = new
            {
                progressPercent = summary.ProgressPercent,
                nextSlabTarget = summary.CurrentSale + summary.AdditionalPurchaseRequired,
                remainingGap = summary.AdditionalPurchaseRequired,
                projectedIncentiveAtNextSlab = summary.NextIncentive
            },
            historicalTimeline12Months = history
        });
    }

    // =========================================================================
    // 8. POST /api/incentive/lock
    // =========================================================================
    [HttpPost("incentive/lock")]
    [Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
    public async Task<IActionResult> LockMonth([FromQuery] int month, [FromQuery] int year, CancellationToken cancellationToken)
    {
        if (month < 1 || month > 12 || year < 2000)
            return BadRequest("Invalid month or year values.");

        var updatedRows = await db.SsIncentives
            .Where(x => x.Month == month && x.Year == year && !x.IsDeleted)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, "Posted"), cancellationToken);

        if (updatedRows == 0)
            return NotFound("No incentive records found for the specified period.");

        var period = await db.IncentivePeriods.FirstOrDefaultAsync(x => x.Month == month && x.Year == year, cancellationToken);
        if (period == null)
        {
            period = new IncentivePeriod
            {
                Month = month,
                Year = year,
                SourceType = "Dynamic",
                Status = "Locked",
                LockedFlag = true,
                LockedBy = User.Identity?.Name ?? "system",
                LockedDate = DateTime.UtcNow
            };
            db.IncentivePeriods.Add(period);
        }
        else
        {
            period.LockedFlag = true;
            period.Status = "Locked";
            period.LockedBy = User.Identity?.Name ?? "system";
            period.LockedDate = DateTime.UtcNow;
        }

        var schemes = await db.IncentiveSchemes
            .Where(x => x.SchemeMonth == month && x.SchemeYear == year)
            .ToListAsync(cancellationToken);
        foreach (var sc in schemes)
            sc.IsLocked = true;

        await db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            ok = true,
            message = $"Successfully locked sales ledger, incentive schemes, and calculations for {new DateTime(year, month, 1):MMMM yyyy}."
        });
    }
}
