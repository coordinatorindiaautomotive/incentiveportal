using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Services;
using IncentivePortal.Models;

namespace IncentivePortal.Controllers;

/// <summary>
/// Controller responsible for managing bank transfer payouts, UTR updates, and bank reconciliation statements.
/// Restricts access to Super Admin, HO Finance, and Auditor roles.
/// </summary>
[Authorize(Roles = "Super Admin,HO Finance,Auditor")]
public sealed class TransfersController(ITransferService transferService) : Controller
{
    /// <summary>
    /// Renders the list of all bank transfer entries, detailing calculation logs, dealer account details, and statuses.
    /// </summary>
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var (defaultMonth, defaultYear, incentives) = await transferService.GetIndexDataAsync(cancellationToken);
        ViewBag.DefaultMonth = defaultMonth;
        ViewBag.DefaultYear = defaultYear;
        return View(incentives);
    }

    /// <summary>
    /// Manually reconciles a single transfer payout by setting its UTR number and updating its status.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> Reconcile(int id, string utr, CancellationToken cancellationToken)
    {
        try
        {
            await transferService.ReconcileAsync(id, utr, cancellationToken);
            return Json(new { ok = true, message = "Transfer reconciled." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Bulk reconciles multiple selected transfer payouts using a shared UTR transaction reference.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> BulkReconcile(List<int> ids, string utr, CancellationToken cancellationToken)
    {
        try
        {
            await transferService.BulkReconcileAsync(ids, utr, cancellationToken);
            return Json(new { ok = true, message = "Successfully reconciled transfers." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Processes an uploaded bank statement Excel workbook to reconcile payouts.
    /// Performs exact or partial dealer matching based on Beneficiary Accounts and Narration.
    /// Auto-registers dynamic voucher payouts if bank transactions are found for unrecorded active dealers.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> UploadReconciliation(
        IFormFile file, 
        int? reconcileMonth, 
        int? reconcileYear, 
        int? mapAccountNo,
        int? mapStatus,
        int? mapUtr,
        int? mapPartyCode,
        int? mapName,
        int? mapIfsc,
        int? mapAmount,
        int? mapDate,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a valid Excel file." });

        try
        {
            var username = User.Identity?.Name ?? "system";
            var result = await transferService.UploadReconciliationAsync(
                file, reconcileMonth, reconcileYear, mapAccountNo, mapStatus, mapUtr,
                mapPartyCode, mapName, mapIfsc, mapAmount, mapDate, username, cancellationToken);
            return Json(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Reads and parses the headers of the uploaded Excel sheet.
    /// Used by manual mapping wizard on the UI.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ParseHeaders(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a valid Excel file." });

        try
        {
            var headers = await transferService.ParseHeadersAsync(file, cancellationToken);
            return Json(new { ok = true, headers = headers });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ConfirmAutoCreatedPayouts([FromBody] List<ConfirmPayoutDto> models, CancellationToken cancellationToken)
    {
        if (models == null || models.Count == 0)
        {
            return BadRequest(new { message = "No payout records to create." });
        }

        try
        {
            var username = User.Identity?.Name ?? "system";
            var result = await transferService.ConfirmAutoCreatedPayoutsAsync(models, username, cancellationToken);
            return Json(new { ok = true, message = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Database error: " + (ex.InnerException?.Message ?? ex.Message) });
        }
    }

    /// <summary>
    /// Fetches all unmatched bank statement rows for a given period.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor")]
    public async Task<IActionResult> GetUnmatchedRecords(int month, int year, CancellationToken cancellationToken)
    {
        var records = await transferService.GetUnmatchedRecordsAsync(month, year, cancellationToken);
        return Json(records);
    }

    /// <summary>
    /// Updates the statement record properties (Name, Account, IFSC, Amount, UTR, Status, Date, PartyCode).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SaveUnmatchedRecord([FromBody] BankStatementRecord model, CancellationToken cancellationToken)
    {
        if (model == null)
            return BadRequest(new { message = "Invalid data." });

        try
        {
            var username = User.Identity?.Name ?? "system";
            await transferService.SaveUnmatchedRecordAsync(model, username, cancellationToken);
            return Json(new { ok = true, message = "Record updated successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Statement record not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    /// <summary>
    /// Soft-deletes an unmatched statement record from the manual registry.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> DeleteUnmatchedRecord(int id, CancellationToken cancellationToken)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            await transferService.DeleteUnmatchedRecordAsync(id, username, cancellationToken);
            return Json(new { ok = true, message = "Record deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Statement record not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    /// <summary>
    /// Manually creates a new unmatched bank statement record in the registry.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> CreateUnmatchedRecord([FromBody] BankStatementRecord model, CancellationToken cancellationToken)
    {
        if (model == null)
            return BadRequest(new { message = "Invalid data." });

        try
        {
            var username = User.Identity?.Name ?? "system";
            await transferService.CreateUnmatchedRecordAsync(model, username, cancellationToken);
            return Json(new { ok = true, message = "Record created successfully." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    /// <summary>
    /// Gets active parties list for manual mapping dropdown selection.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor")]
    public async Task<IActionResult> GetPartiesList(CancellationToken cancellationToken)
    {
        var list = await transferService.GetPartiesListAsync(cancellationToken);
        return Json(list);
    }

    /// <summary>
    /// Re-runs the reconciliation matching pass for the given month/year without requiring a
    /// new file upload. Uses existing BankStatementRecords to update PaymentStatus and PaymentDate,
    /// and marks outstanding parties without a bank match as "Credit Party".
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> RunReconciliation(int month, int year, CancellationToken cancellationToken)
    {
        try
        {
            var username = User.Identity?.Name ?? "system";
            var result = await transferService.RunReconciliationAsync(month, year, username, cancellationToken);
            return Json(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    /// <summary>
    /// Reconciles an unmatched statement record manually by assigning it to a dealer.
    /// Updates or creates the dealer's SsIncentive payout for the target month/year.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ReconcileUnmatchedRecord(int id, string partyCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(partyCode))
            return BadRequest(new { message = "Please select a dealer." });

        try
        {
            var username = User.Identity?.Name ?? "system";
            await transferService.ReconcileUnmatchedRecordAsync(id, partyCode, username, cancellationToken);
            return Json(new { ok = true, message = "Statement reconciled to dealer successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }
}
