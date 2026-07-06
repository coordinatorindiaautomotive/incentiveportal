using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Services;

namespace IncentivePortal.Controllers;

[Authorize]
[ApiController]
[Route("Dashboard")]
public sealed class DashboardApiController(
    IDashboardQueriesService dashboardQueriesService,
    IReportBuilderService reportBuilder
) : ControllerBase
{
    [HttpGet("GetKPIs")]
    public async Task<IActionResult> GetKPIs(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetKPIsAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetTrend")]
    public async Task<IActionResult> GetTrend(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        [FromQuery] string? view,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetTrendAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, view, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetCategoryMix")]
    public async Task<IActionResult> GetCategoryMix(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetCategoryMixAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetPartyMix")]
    public async Task<IActionResult> GetPartyMix(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetPartyMixAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetConsigneeMix")]
    public async Task<IActionResult> GetConsigneeMix(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetConsigneeMixAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetDealerSubTypeMix")]
    public async Task<IActionResult> GetDealerSubTypeMix(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetDealerSubTypeMixAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetQuarterSummary")]
    public async Task<IActionResult> GetQuarterSummary(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetQuarterSummaryAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetLocationRanking")]
    public async Task<IActionResult> GetLocationRanking(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetLocationRankingAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetComparison")]
    public async Task<IActionResult> GetComparison(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        [FromQuery] string? cmp,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetComparisonAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cmp, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetConsigneeSummary")]
    public async Task<IActionResult> GetConsigneeSummary(
        [FromQuery] string? yr,
        [FromQuery] string? met,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? consignees,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? locations,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await dashboardQueriesService.GetConsigneeSummaryAsync(yr, met, quarters, months, partyTypes, categories, consignees, dealerSubTypes, locations, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetDealerTable")]
    public async Task<IActionResult> GetDealerTable(
        [FromQuery] int targetYear,
        [FromQuery] string? quarters,
        [FromQuery] string? months,
        [FromQuery] string? partyTypes,
        [FromQuery] string? categories,
        [FromQuery] string? locations,
        [FromQuery] string? dealerSubTypes,
        [FromQuery] string? search,
        [FromQuery] string? limit,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await reportBuilder.GetDealerSalesAsync(targetYear, quarters, months, partyTypes, categories, locations, dealerSubTypes, search, limit, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
