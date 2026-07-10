using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using IncentivePortal.Services;
using IncentivePortal.Helpers;
using IncentivePortal.DTOs;
using IncentivePortal.Data;

using Microsoft.Extensions.Caching.Memory;

namespace IncentivePortal.Controllers;

/// <summary>
/// Controller responsible for importing and validating transactional monthly sales spreadsheets.
/// Access is restricted to Super Admin and HO Finance roles.
/// </summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class ImportsController(
    IImportsAppService importsAppService,
    ICurrentUser currentUser,
    IMemoryCache memoryCache) : Controller
{
    // =========================================================
    // MONTHLY SALES PAGE
    // =========================================================
    public async Task<IActionResult> MonthlySales(CancellationToken cancellationToken)
    {
        var (branches, alternateMappings) = await importsAppService.GetMonthlySalesDataAsync(cancellationToken);
        ViewBag.Branches = branches;
        ViewBag.AlternateMappings = alternateMappings;
        return View();
    }

    // =========================================================
    // DOWNLOAD MONTHLY TEMPLATE
    // =========================================================
    [HttpGet]
    public IActionResult DownloadMonthlyTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Monthly Sales");

        var currentPeriodStr = DateTime.Today.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        var currentMonthStr = DateTime.Today.ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
        var currentYearStr = DateTime.Today.Year.ToString();

        string[] headers =
        {
            "Consignee", "Dealer Code", "Loc", "Part Category Code", "Part Num",
            "Root Part Num", "Day", "Fiscal Year", "Month", "Month Year",
            "Cons Party Code", "Cons Party Name"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
        }

        // Add dummy instruction row
        ws.Cell(2, 1).Value = "Example Client Name";
        ws.Cell(2, 2).Value = "DLR001";
        ws.Cell(2, 3).Value = "RJ06";
        ws.Cell(2, 4).Value = "CAT01";
        ws.Cell(2, 5).Value = "PN-999";
        ws.Cell(2, 6).Value = "PN-999-R";
        ws.Cell(2, 7).Value = 1;
        ws.Cell(2, 8).Value = currentYearStr;
        ws.Cell(2, 9).Value = DateTime.Today.Month;
        ws.Cell(2, 10).Value = currentPeriodStr;
        ws.Cell(2, 11).Value = "DLR001";
        ws.Cell(2, 12).Value = "Example Client Name";

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Monthly_Sales_Template_{currentMonthStr}_{currentYearStr}.xlsx");
    }

    // =========================================================
    // PARSE EXCEL METADATA
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> ParseExcelMetadata(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a valid Excel file." });

            var result = await importsAppService.ParseExcelMetadataAsync(file, currentUser, cancellationToken);
            return Json(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // PREVIEW IMPORT
    // =========================================================
    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
    public async Task<IActionResult> Preview(
        IFormFile file,
        [FromForm] string uploadMode,
        [FromForm] string? branchRulesJson,
        [FromForm] string? alternateCodesJson,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a valid Excel file." });

            var (previewToken, displayRows) = await importsAppService.PreviewAsync(
                file, uploadMode, branchRulesJson, alternateCodesJson, currentUser, cancellationToken);

            return Json(new { previewToken, rows = displayRows });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // COMMIT IMPORT
    // =========================================================
    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
    public async Task<IActionResult> Commit(
        IFormFile file,
        [FromForm] string uploadMode,
        [FromForm] string? branchRulesJson,
        [FromForm] string? alternateCodesJson,
        [FromForm] string? changeReason,
        [FromForm] int? previousImportLogId,
        [FromForm] string? previewToken,
        [FromForm] bool rewriteSales = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Please select a valid Excel file." });

            var jobId = await importsAppService.CommitAsync(
                file, uploadMode, branchRulesJson, alternateCodesJson, changeReason,
                previousImportLogId, previewToken, rewriteSales, currentUser, cancellationToken);

            return Json(new
            {
                ok = true,
                jobId = jobId,
                message = "Import processing has been offloaded to a background task."
            });
        }
        catch (Exception ex)
        {
            var fullMsg = ex.Message;
            if (ex.InnerException != null) fullMsg += " | Inner: " + ex.InnerException.Message;
            return BadRequest(new { message = fullMsg });
        }
    }

    // =========================================================
    // GET CALC METADATA
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetCalcMetadata(int month, int year, CancellationToken cancellationToken)
    {
        try
        {
            var (locations, categories, partyTypes) = await importsAppService.GetCalcMetadataAsync(month, year, cancellationToken);
            var (branches, alternateMappings) = await importsAppService.GetMonthlySalesDataAsync(cancellationToken);
            
            var seededBranches = branches.ToDictionary(
                x => x.Code, 
                x => new { allowedCategories = x.AllowedCategories, allowedPartyTypes = x.AllowedPartyTypes }, 
                StringComparer.OrdinalIgnoreCase);
            
            var seededPartyMappings = alternateMappings;

            return Json(new
            {
                locations,
                categories,
                partyTypes,
                seededBranches,
                seededPartyMappings
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // CALCULATE INCENTIVE
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Calculate(
        int month,
        int year,
        bool forceRecalculate = true,
        [FromForm] string? branchRulesJson = null,
        [FromForm] string? partyMappingsJson = null,
        [FromForm] string? governorFiltersJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var jobId = await importsAppService.RunCalculationJobAsync(
                month, year, forceRecalculate, branchRulesJson, partyMappingsJson, governorFiltersJson, currentUser, cancellationToken);

            return Json(new
            {
                ok = true,
                jobId = jobId,
                message = "Incentive calculations have been offloaded to a background task."
            });
        }
        catch (Exception ex)
        {
            var fullMsg = ex.Message;
            if (ex.InnerException != null) fullMsg += " | " + ex.InnerException.Message;
            return BadRequest(new { ok = false, message = fullMsg });
        }
    }

    // =========================================================
    // PREVIEW CALCULATION
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> PreviewCalculation(
        int month,
        int year,
        bool forceRecalculate = true,
        [FromForm] string? branchRulesJson = null,
        [FromForm] string? partyMappingsJson = null,
        [FromForm] string? governorFiltersJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var previewRows = await importsAppService.PreviewCalculationAsync(
                month, year, forceRecalculate, branchRulesJson, partyMappingsJson, governorFiltersJson, currentUser, cancellationToken);
            return Json(previewRows);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
            return BadRequest(new { ok = false, message = msg });
        }
    }

    // =========================================================
    // PAYOUT ENGINE CRUD — GET SALE ROW
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetSaleRow(string partyCode, int month, int year, CancellationToken ct)
    {
        try
        {
            var result = await importsAppService.GetSaleRowAsync(partyCode, month, year, currentUser, ct);
            return Json(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // PAYOUT ENGINE CRUD — EDIT SALE ROW
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSaleRow(int id, decimal saleValue, decimal discount, string? remarks, CancellationToken ct)
    {
        try
        {
            await importsAppService.EditSaleRowAsync(id, saleValue, discount, remarks, currentUser, ct);
            return Json(new
            {
                ok = true,
                message = "Incentive record updated. Re-run preview to see recalculated incentive."
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Incentive record not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
            return BadRequest(new { message = msg });
        }
    }

    // =========================================================
    // PAYOUT ENGINE CRUD — DELETE SALE ROW
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSaleRow(int id, CancellationToken ct)
    {
        try
        {
            await importsAppService.DeleteSaleRowAsync(id, currentUser, ct);
            return Json(new
            {
                ok = true,
                message = "Incentive record deleted. Re-run preview to update totals."
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Incentive record not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
            return BadRequest(new { message = msg });
        }
    }

    // =========================================================
    // EXPORT CALCULATION PREVIEW TO EXCEL
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> ExportCalculationPreview(
        int month,
        int year,
        bool forceRecalculate = true,
        string? branchRulesJson = null,
        string? partyMappingsJson = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var excelContent = await importsAppService.ExportCalculationPreviewAsync(
                month, year, forceRecalculate, branchRulesJson, partyMappingsJson, currentUser, cancellationToken);

            var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
            var monthStr = (month >= 1 && month <= 12) ? monthNames[month - 1] : month.ToString();
            return File(excelContent, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Calculation_Preview_{monthStr}_{year}.xlsx");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // =========================================================
    // APPROVE CALCULATION FOR MONTH
    // =========================================================
    [AllowAnonymous]
    [HttpGet]
    [Route("/RunCalculationTest")]
    public async Task<IActionResult> RunCalculationTest(int month, int year, CancellationToken ct)
    {
        var jobId = await importsAppService.RunCalculationJobAsync(
            month, year, true, null, null, null, currentUser, ct);
        return Ok(new { jobId = jobId, status = "Started calculation" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCalculation(int month, int year, CancellationToken ct)
    {
        try
        {
            var count = await importsAppService.ApproveCalculationAsync(month, year, ct);
            if (count == 0)
            {
                return Json(new { ok = false, message = "No pending records found for the selected period." });
            }
            return Json(new { ok = true, message = $"Successfully approved and posted {count} incentive records." });
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += " | " + ex.InnerException.Message;
            return BadRequest(new { ok = false, message = msg });
        }
    }

    // =========================================================
    // GET JOB STATUS
    // =========================================================
    [HttpGet]
    public IActionResult GetJobStatus(string jobId)
    {
        if (memoryCache.TryGetValue($"JobStatus_{jobId}", out BackgroundJobState? state))
        {
            return Json(state);
        }
        return NotFound(new { message = "Job not found." });
    }
}