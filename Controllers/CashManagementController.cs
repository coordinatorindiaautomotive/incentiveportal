using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Models;
using IncentivePortal.Services;
using IncentivePortal.Helpers;

namespace IncentivePortal.Controllers;

[Authorize]
public class CashManagementController : Controller
{
    private readonly ICashManagementService _cashManagementService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

    public CashManagementController(ICashManagementService cashManagementService, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _cashManagementService = cashManagementService;
        _configuration = configuration;
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────
    private IActionResult RedirectToSource(string defaultAction)
    {
        var referer = Request.Headers["Referer"].ToString();
        if (!string.IsNullOrEmpty(referer) && referer.Contains("CashBook", StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(CashBook));
        }
        return RedirectToAction(defaultAction);
    }

    // ─────────────────────────────────────────────
    // CASH IN — LIST
    // ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> CashIn(string? status, int? branchId, DateTime? from, DateTime? to)
    {
        var model = await _cashManagementService.GetCashInListAsync(status, branchId, from, to, HttpContext.RequestAborted);

        ViewData["Title"]   = "Cash In";
        ViewData["Branches"]= model.Branches;

        ViewBag.FilterStatus   = status;
        ViewBag.FilterBranchId = branchId;
        ViewBag.FilterFrom     = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo       = to?.ToString("yyyy-MM-dd");
        ViewBag.TotalAmount    = model.TotalAmount;
        ViewBag.TotalCount     = model.TotalCount;
        ViewBag.ReceiptTypes   = model.ReceiptTypes;
        ViewBag.Parties        = model.Parties;

        return View(model.Transactions);
    }

    // ─────────────────────────────────────────────
    // CASH IN — CREATE
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCashIn(CashInTransaction model, string action, IFormFile? AttachmentFile)
    {
        bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                   || Request.Headers["Accept"].ToString().Contains("application/json")
                   || Request.ContentType?.Contains("multipart/form-data") == true && !Request.Headers.ContainsKey("Upgrade-Insecure-Requests");

        // Detect AJAX from CashBook page (fetch sends no Upgrade-Insecure-Requests header)
        bool isFetchRequest = !Request.Headers.ContainsKey("Upgrade-Insecure-Requests") && Request.Method == "POST";

        if (await _cashManagementService.IsCurrentPeriodLocked(HttpContext.RequestAborted))
        {
            if (isFetchRequest)
                return Json(new { success = false, message = "Period is locked. Contact HO Finance." });
            TempData["Error"] = "Period is locked. Contact HO Finance.";
            return RedirectToSource(nameof(CashIn));
        }

        string? attachmentPath = null;
        if (AttachmentFile != null && AttachmentFile.Length > 0)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cash");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(AttachmentFile.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await AttachmentFile.CopyToAsync(stream);
            }
            attachmentPath = $"/uploads/cash/{fileName}";
        }

        try
        {
            await _cashManagementService.CreateOrUpdateCashInAsync(model, action, attachmentPath, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            
            if (isFetchRequest)
                return Json(new { success = true, message = model.Id > 0 ? "Transaction updated." : "Transaction saved." });

            TempData["Success"] = model.Id > 0 
                ? $"Cash In {model.TransactionNo} updated successfully."
                : $"Cash In {model.TransactionNo} saved ({model.Status}).";
        }
        catch (UnauthorizedAccessException ex)
        {
            if (isFetchRequest)
                return Json(new { success = false, message = ex.Message.Length > 0 ? ex.Message : "Unauthorized action." });
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            if (isFetchRequest)
                return Json(new { success = false, message = "Transaction not found." });
            return NotFound();
        }
        catch (Exception ex)
        {
            if (isFetchRequest)
                return Json(new { success = false, message = ex.Message });
            TempData["Error"] = ex.Message;
        }

        return RedirectToSource(nameof(CashIn));
    }


    // ─────────────────────────────────────────────
    // CASH IN — APPROVE / REJECT
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCashIn(int id, string newStatus, string? remarks)
    {
        try
        {
            await _cashManagementService.ApproveCashInAsync(id, newStatus, remarks, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = $"{newStatus} status set successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToSource(nameof(CashIn));
    }

    // ─────────────────────────────────────────────
    // CASH IN — DELETE (draft only)
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCashIn(int id)
    {
        try
        {
            await _cashManagementService.DeleteCashInAsync(id, HttpContext.RequestAborted);
            TempData["Success"] = "Draft deleted.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException)
        {
            return BadRequest();
        }
        return RedirectToSource(nameof(CashIn));
    }

    // ─────────────────────────────────────────────
    // CASH OUT — LIST
    // ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> CashOut(string? status, int? branchId, DateTime? from, DateTime? to)
    {
        var model = await _cashManagementService.GetCashOutListAsync(status, branchId, from, to, HttpContext.RequestAborted);

        ViewData["Title"]   = "Cash Out";
        ViewData["Branches"]= model.Branches;

        ViewBag.FilterStatus   = status;
        ViewBag.FilterBranchId = branchId;
        ViewBag.FilterFrom     = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo       = to?.ToString("yyyy-MM-dd");
        ViewBag.TotalAmount    = model.TotalAmount;
        ViewBag.TotalCount     = model.TotalCount;
        ViewBag.ExpenseCategories = model.ExpenseCategories;

        return View(model.Transactions);
    }

    // ─────────────────────────────────────────────
    // CASH OUT — CREATE
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCashOut(CashOutTransaction model, string action, IFormFile? AttachmentFile)
    {
        if (await _cashManagementService.IsCurrentPeriodLocked(HttpContext.RequestAborted))
        {
            return Json(new { success = false, message = "Period is locked. Contact HO Finance." });
        }

        string? attachmentPath = null;
        if (AttachmentFile != null && AttachmentFile.Length > 0)
        {
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cash");
            if (!Directory.Exists(uploadsDir))
            {
                Directory.CreateDirectory(uploadsDir);
            }
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(AttachmentFile.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await AttachmentFile.CopyToAsync(stream);
            }
            attachmentPath = $"/uploads/cash/{fileName}";
        }

        try
        {
            await _cashManagementService.CreateOrUpdateCashOutAsync(model, action, attachmentPath, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = model.Id > 0 
                ? $"Cash Out {model.TransactionNo} updated successfully."
                : $"Cash Out {model.TransactionNo} saved ({model.Status}).";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToSource(nameof(CashOut));
    }

    // ─────────────────────────────────────────────
    // CASH OUT — APPROVE / REJECT
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCashOut(int id, string newStatus, string? remarks)
    {
        try
        {
            await _cashManagementService.ApproveCashOutAsync(id, newStatus, remarks, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = $"{newStatus} status set successfully.";
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToSource(nameof(CashOut));
    }

    // ─────────────────────────────────────────────
    // RECONCILIATION CONSOLE
    // ─────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor")]
    public async Task<IActionResult> Reconciliation(string? reconStatus, int? branchId, int? year, int? month)
    {
        var model = await _cashManagementService.GetReconciliationAsync(reconStatus, branchId, year, month, HttpContext.RequestAborted);

        ViewData["Title"]   = "Reconciliation Console";
        ViewData["Branches"]= model.Branches;

        ViewBag.TotalCount    = model.TotalCount;
        ViewBag.MatchedCount  = model.MatchedCount;
        ViewBag.PartialCount  = model.PartialCount;
        ViewBag.MissTally     = model.MissTally;
        ViewBag.MissPortal    = model.MissPortal;
        ViewBag.FilterStatus  = reconStatus;
        ViewBag.FilterBranch  = branchId;
        ViewBag.ResolvedYear  = model.ResolvedYear;
        ViewBag.ResolvedMonth = model.ResolvedMonth;

        var reconConfig = _configuration.GetSection("Reconciliation");
        ViewBag.ExactTolerance = reconConfig.GetValue<decimal>("ExactMatchToleranceAmount", 1.00m);
        ViewBag.PartialToleranceAmt = reconConfig.GetValue<decimal>("PartialMatchToleranceAmount", 500.00m);
        ViewBag.PartialTolerancePct = reconConfig.GetValue<decimal>("PartialMatchTolerancePercent", 5.0m);
        ViewBag.IsTallyConfigured = !string.IsNullOrEmpty(_configuration.GetConnectionString("TallyDbConnection"));

        return View(model.Records);
    }

    // ─────────────────────────────────────────────
    // RECONCILIATION — AUTO MATCH
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> AutoMatch(int year, int month)
    {
        try
        {
            var result = await _cashManagementService.AutoMatchAsync(year, month, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = $"Auto-match complete. Matched: {result.Matched}, Partial: {result.Partial}, Missing Tally: {result.MissingTally}, Missing Portal: {result.MissingPortal}. Total processed: {result.TotalProcessed}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Reconciliation), new { year, month });
    }

    // ─────────────────────────────────────────────
    // RECONCILIATION — APPROVE SINGLE
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ApproveRecon(int id, string? remarks)
    {
        try
        {
            await _cashManagementService.ApproveReconAsync(id, remarks, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = "Reconciliation approved.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Reconciliation));
    }

    // ─────────────────────────────────────────────
    // RECONCILIATION — MANUAL MATCH
    // ─────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ManualMatch(int? id, string? tallyVoucherNo, decimal? tallyAmount, int? cashInId, int? cashOutId, string? remarks)
    {
        try
        {
            if (Request.ContentType != null && Request.ContentType.Contains("application/json"))
            {
                using var reader = new StreamReader(Request.Body);
                var bodyText = await reader.ReadToEndAsync();
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var jsonRequest = System.Text.Json.JsonSerializer.Deserialize<ManualMatchJsonDto>(bodyText, options);
                if (jsonRequest != null)
                {
                    cashInId = jsonRequest.CashInId;
                    cashOutId = jsonRequest.CashOutId;
                    remarks = jsonRequest.Remarks;
                }
            }

            if (cashInId.HasValue && cashOutId.HasValue)
            {
                await _cashManagementService.ManualMatchTransactionsAsync(cashInId.Value, cashOutId.Value, remarks ?? string.Empty, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
                return Json(new { success = true, message = "Transactions manually matched successfully." });
            }
            else if (id.HasValue && !string.IsNullOrEmpty(tallyVoucherNo) && tallyAmount.HasValue)
            {
                await _cashManagementService.ManualMatchAsync(id.Value, tallyVoucherNo, tallyAmount.Value, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
                return Json(new { success = true, message = "Transaction manually matched successfully." });
            }
            else
            {
                return Json(new { success = false, message = "Invalid parameters. Provide either cashInId and cashOutId, or transaction id and tallyVoucherNo/tallyAmount." });
            }
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public class ManualMatchJsonDto
    {
        public int? CashInId { get; set; }
        public int? CashOutId { get; set; }
        public string? Remarks { get; set; }
    }

    // ─────────────────────────────────────────────
    // RECONCILIATION — VERIFY WITH TALLY
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> VerifyTally(int id, string type, string tallyStatus, string? tallyVoucherNo, string? remarks)
    {
        try
        {
            await _cashManagementService.VerifyTallyAsync(id, type, tallyStatus, tallyVoucherNo, remarks, HttpContext.RequestAborted);
            TempData["Success"] = $"{type} verified with Tally ({tallyStatus}).";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToSource(nameof(CashBook));
    }

    // ─────────────────────────────────────────────
    // EXCEPTIONS
    // ─────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> Exceptions(string? severity, string? exStatus)
    {
        var model = await _cashManagementService.GetExceptionsAsync(severity, exStatus, HttpContext.RequestAborted);

        ViewData["Title"] = "Exception Management";

        ViewBag.Critical     = model.Critical;
        ViewBag.High         = model.High;
        ViewBag.Medium       = model.Medium;
        ViewBag.Low          = model.Low;
        ViewBag.OpenCount    = model.OpenCount;
        ViewBag.FilterSev    = severity;
        ViewBag.FilterStatus = exStatus;

        return View(model.Exceptions);
    }

    // ─────────────────────────────────────────────
    // EXCEPTIONS — RESOLVE
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveException(int id, string resolution)
    {
        try
        {
            await _cashManagementService.ResolveExceptionAsync(id, resolution, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = "Exception resolved.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Exceptions));
    }

    // ─────────────────────────────────────────────
    // EXCEPTIONS — ESCALATE
    // ─────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EscalateException(int id)
    {
        try
        {
            await _cashManagementService.EscalateExceptionAsync(id, HttpContext.RequestAborted);
            TempData["Success"] = "Exception escalated to HO.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Exceptions));
    }

    // ─────────────────────────────────────────────
    // CASH BOOK — EXCEL STYLE LEDGER
    // ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> CashBook(int? branchId, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var startDate = from ?? new DateTime(today.Year, today.Month, 1);
        var endDate = to ?? today;

        var model = await _cashManagementService.GetCashBookAsync(branchId, from, to, HttpContext.RequestAborted);

        ViewData["Title"]   = "Cash Book";
        ViewData["Branches"]= model.Branches;

        ViewBag.FilterBranchId = branchId;
        ViewBag.FilterFrom     = startDate.ToString("yyyy-MM-dd");
        ViewBag.FilterTo       = endDate.ToString("yyyy-MM-dd");

        ViewBag.RegisterCashIns = model.RegisterCashIns;
        ViewBag.RegisterCashOuts = model.RegisterCashOuts;
        ViewBag.ReceiptTypes = model.ReceiptTypes;
        ViewBag.ExpenseCategories = model.ExpenseCategories;
        ViewBag.Parties = model.Parties;

        return View(model.CashBook);
    }

    [HttpGet]
    public async Task<IActionResult> PeriodStatus(CancellationToken cancellationToken)
    {
        var isLocked = await _cashManagementService.IsCurrentPeriodLocked(cancellationToken);
        return Json(new { isLocked = isLocked });
    }

    // ─────────────────────────────────────────────
    // PERIOD CONTROLS
    // ─────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> PeriodControls()
    {
        ViewData["Title"] = "Period Controls";
        var periods = await _cashManagementService.GetPeriodControlsAsync(HttpContext.RequestAborted);
        return View(periods);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> OpenNewPeriod(int year, int month)
    {
        try
        {
            await _cashManagementService.OpenNewPeriodAsync(year, month, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = $"Period {year}/{month:D2} opened.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(PeriodControls));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ClosePeriod(int year, int month)
    {
        try
        {
            await _cashManagementService.ClosePeriodAsync(year, month, User.Identity?.Name ?? "system", HttpContext.RequestAborted);
            TempData["Success"] = $"Period {year}/{month:D2} closed.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(PeriodControls));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin")]
    public async Task<IActionResult> LockPeriod(int year, int month)
    {
        try
        {
            await _cashManagementService.LockPeriodAsync(year, month, HttpContext.RequestAborted);
            TempData["Success"] = $"Period {year}/{month:D2} permanently locked.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(PeriodControls));
    }

    // ─────────────────────────────────────────────
    // CASH MASTERS MANAGEMENT
    // ─────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> Masters()
    {
        ViewData["Title"] = "Cash Masters";
        
        var model = await _cashManagementService.GetMastersAsync(HttpContext.RequestAborted);
            
        ViewBag.ReceiptTypes = model.ReceiptTypes;
        ViewBag.ExpenseCategories = model.ExpenseCategories;
        
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SaveMasterItem(int id, string itemType, string name)
    {
        try
        {
            await _cashManagementService.SaveMasterItemAsync(id, itemType, name, HttpContext.RequestAborted);
            TempData["Success"] = "Cash Master item saved successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Masters));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> ToggleMasterItemActive(int id)
    {
        try
        {
            await _cashManagementService.ToggleMasterItemActiveAsync(id, HttpContext.RequestAborted);
            TempData["Success"] = "Cash Master item status updated successfully.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Masters));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> DeleteMasterItem(int id)
    {
        try
        {
            await _cashManagementService.DeleteMasterItemAsync(id, HttpContext.RequestAborted);
            TempData["Success"] = "Cash Master item deleted successfully.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        return RedirectToAction(nameof(Masters));
    }

    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> CostCenterCash(int? month, int? year, CancellationToken cancellationToken)
    {
        if (!month.HasValue || !year.HasValue)
        {
            var latest = await _cashManagementService.GetPeriodControlsAsync(cancellationToken);
            var latestOpen = latest.FirstOrDefault(x => x.Status == "Open") ?? latest.FirstOrDefault();
            if (latestOpen != null)
            {
                month = latestOpen.ControlMonth;
                year = latestOpen.ControlYear;
            }
            else
            {
                month = DateTime.Today.Month;
                year = DateTime.Today.Year;
            }
        }

        ViewBag.SelectedMonth = month.Value;
        ViewBag.SelectedYear = year.Value;

        var list = await _cashManagementService.GetCostCenterCashListAsync(month.Value, year.Value, cancellationToken);
        return View(list);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SyncCostCenterCash(int? month, int? year, CancellationToken cancellationToken)
    {
        try
        {
            var syncLogs = await _cashManagementService.SyncCostCenterCashAsync(month, year, cancellationToken);
            return Json(new { ok = true, message = "Successfully synchronized Cost Center Cash in Hand balances with Tally ERP 9.", logs = syncLogs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Sync failed: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetDmsSale(int branchId, DateTime date)
    {
        var result = await _cashManagementService.GetDmsSaleAsync(branchId, date, HttpContext.RequestAborted);
        if (result.success)
        {
            return Json(new { success = true, totalSale = result.totalSale, totalInvoices = result.totalInvoices, branchCode = result.branchCode });
        }
        else
        {
            return Json(new { success = false, message = result.message });
        }
    }
}
