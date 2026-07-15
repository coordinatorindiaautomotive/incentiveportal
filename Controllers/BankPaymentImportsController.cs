using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IncentivePortal.Application.CashManagement.Services;
using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

/// <summary>
/// Manages the Protected Raw Bank Payment Table (SSOT).
/// Handles upload, preview, batch history and drill-down.
/// Force re-import is restricted to Super Admin.
/// </summary>
[Authorize(Roles = $"{AppRoles.SuperAdmin},{AppRoles.HOFinance}")]
public sealed class BankPaymentImportsController(
    IBankPaymentImportService bankPaymentImportService,
    ICurrentUser currentUser) : Controller
{
    private const int PageSize = 50;

    // =========================================================
    // INDEX — upload history
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> Index(
        string? status, string? from, string? to,
        CancellationToken cancellationToken)
    {
        DateTime? fromDate = DateTime.TryParse(from, out var fd) ? fd : null;
        DateTime? toDate   = DateTime.TryParse(to,   out var td) ? td : null;

        var batches = await bankPaymentImportService.GetBatchesAsync(fromDate, toDate, status, cancellationToken);

        ViewBag.StatusFilter = status;
        ViewBag.FromFilter   = from;
        ViewBag.ToFilter     = to;
        ViewBag.Statuses     = new[] {
            BankPaymentImportStatus.Pending,
            BankPaymentImportStatus.Completed,
            BankPaymentImportStatus.PartialSuccess,
            BankPaymentImportStatus.Failed
        };

        return View(batches);
    }

    // =========================================================
    // UPLOAD PAGE
    // =========================================================
    [HttpGet]
    public IActionResult Upload() => View();

    // =========================================================
    // DOWNLOAD TEMPLATE
    // =========================================================
    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var ws = workbook.Worksheets.Add("Bank Payments");

        string[] headers =
        {
            "File_Sequence_Num", "Pymt_Prod_Type_Code", "Pymt_Mode",
            "Debit_Acct_no", "Beneficiary Name", "Beneficiary Account No",
            "Bene_IFSC_Code", "Amount", "Debit Narration", "Credit Narration",
            "Mobile Number", "Email ID", "Remark", "Pymt_Date", "Reference_no",
            "Addl_Info1", "Addl_Info2", "Addl_Info3", "Addl_Info4", "Addl_Info5",
            "Beneficiary LEI", "STATUS", "Current Step", "File Name",
            "Rejected By", "Rejection Reason", "Acct_Debit_Date",
            "Customer Ref No", "UTR NO"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
            ws.Cell(1, i + 1).Style.Font.Bold = true;
            ws.Cell(1, i + 1).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"BankPaymentTemplate_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    // =========================================================
    // PREVIEW (AJAX POST — multipart)
    // =========================================================
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB
    public async Task<IActionResult> Preview(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, error = "No file uploaded." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await bankPaymentImportService.PreviewAsync(stream, file.FileName, cancellationToken);

            return Json(new
            {
                success          = result.IsValid,
                totalRows        = result.TotalRows,
                previewRows      = result.Rows,
                isDuplicateFile  = result.IsDuplicateFile,
                duplicateBatchRef = result.DuplicateFileBatchRef,
                warnings         = result.Warnings,
                errors           = result.Errors
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = $"Excel parsing failed: {ex.Message}", errors = new[] { ex.Message } });
        }
    }

    // =========================================================
    // COMMIT (AJAX POST — multipart)
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> Commit(IFormFile file, int? reconMonth, int? reconYear, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, error = "No file provided." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await bankPaymentImportService.CommitAsync(
                stream, file.FileName, currentUser.UserName, reconMonth, reconYear, cancellationToken);

            return Json(new
            {
                success          = result.Success,
                batchRef         = result.BatchRef,
                batchId          = result.BatchId,
                message          = result.Message,
                totalRecords     = result.TotalRecords,
                importedRecords  = result.ImportedRecords,
                duplicateRecords = result.DuplicateRecords,
                failedRecords    = result.FailedRecords,
                errors           = result.Errors
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = $"An unexpected error occurred during commit: {ex.Message}", errors = new[] { ex.Message } });
        }
    }

    // =========================================================
    // FORCE REIMPORT (SuperAdmin only)
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> ForceReimport(IFormFile file, int? reconMonth, int? reconYear, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, error = "No file provided." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await bankPaymentImportService.ForceReimportAsync(
                stream, file.FileName, currentUser.UserName, reconMonth, reconYear, cancellationToken);

            return Json(new
            {
                success          = result.Success,
                batchRef         = result.BatchRef,
                batchId          = result.BatchId,
                message          = result.Message,
                importedRecords  = result.ImportedRecords,
                duplicateRecords = result.DuplicateRecords,
                errors           = result.Errors
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = $"An unexpected error occurred during force reimport: {ex.Message}", errors = new[] { ex.Message } });
        }
    }

    // =========================================================
    // BATCH DETAIL
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> BatchDetail(int id, int page = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            var vm = await bankPaymentImportService.GetBatchDetailAsync(id, page, PageSize, cancellationToken);
            return View(vm);
        }
        catch (InvalidOperationException)
        {
            TempData["Error"] = "Batch not found.";
            return RedirectToAction(nameof(Index));
        }
    }
}
