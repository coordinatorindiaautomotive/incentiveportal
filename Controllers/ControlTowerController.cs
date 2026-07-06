using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using IncentivePortal.Services;
using IncentivePortal.Models;
using IncentivePortal.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace IncentivePortal.Controllers;

[Authorize(Roles = "Super Admin")]
public sealed class ControlTowerController(IControlTowerService controlTowerService, IncentiveDbContext db) : Controller
{
    /// <summary>
    /// Shared eviction token for all role_perm_* cache entries.
    /// Cancelling it instantly busts every per-user, per-module permission entry.
    /// </summary>
    public static CancellationTokenSource RolePermEvictionSource = new();

    // =========================================================
    // INDEX: MASTER CONSOLE
    // =========================================================
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await controlTowerService.GetIndexDataAsync(cancellationToken);
        
        ViewBag.DecryptedSmtpPassword = data.DecryptedSmtpPassword;
        ViewBag.DecryptedTallyPassword = data.DecryptedTallyPassword;
        ViewBag.NotificationSetting = data.NotificationSetting;
        ViewBag.TallySetting = data.TallySetting;
        ViewBag.RolePermissions = data.RolePermissions;
        ViewBag.AuditLogs = data.AuditLogs;
        ViewBag.HelpTexts = data.HelpTexts;
        ViewBag.ReportColumns = data.ReportColumnConfigs;

        ViewBag.PortalSettings = data.Settings.ToDictionary(s => s.Key, s => s.Value);
        ViewBag.TdsRules = data.TdsRules;
        ViewBag.ColumnMappings = data.ColumnMappings;
        ViewBag.OutstandingRules = data.OutstandingRules;

        // Supported modules and actions for Role Permission Matrix
        ViewBag.ModulesList = new[] 
        { 
            "Ledger", "Reports", "Transfers", "CashManagement", 
            "Parties", "Branches", "Schemes", "Users", 
            "Announcements", "ControlTower", "UploadManagement",
            "Imports", "DataManagement", "PartyExecutive", "ExternalIncentive"
        };
        ViewBag.ActionsList = new[] { "Access", "View", "Edit", "Delete", "Upload" };
        ViewBag.RolesList = new[] 
        { 
            AppRoles.HOFinance, AppRoles.BranchManager, 
            AppRoles.Associate, AppRoles.Auditor, AppRoles.SalesExecutive 
        };

        return View();
    }

    // =========================================================
    // STYLE GUIDE / COMPONENT SHOWCASE
    // =========================================================
    [AllowAnonymous]
    public IActionResult ComponentLibrary()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePortalSettings(Dictionary<string, string> settings, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SavePortalSettingsAsync(settings, cancellationToken);
            return Json(new { ok = true, message = "Portal settings saved successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTdsRule(TdsRule rule, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveTdsRuleAsync(rule, cancellationToken);
            return Json(new { ok = true, message = "TDS rule saved successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "TdsRule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTdsRule(int id, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.DeleteTdsRuleAsync(id, cancellationToken);
            return Json(new { ok = true, message = "TDS rule deleted successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "TdsRule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveColumnMapping(ColumnMappingRule mapping, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveColumnMappingAsync(mapping, cancellationToken);
            return Json(new { ok = true, message = "Column mapping rule saved." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Mapping rule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteColumnMapping(int id, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.DeleteColumnMappingAsync(id, cancellationToken);
            return Json(new { ok = true, message = "Column mapping rule deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Mapping rule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveOutstandingRule(OutstandingRule rule, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveOutstandingRuleAsync(rule, cancellationToken);
            return Json(new { ok = true, message = "Outstanding rule saved." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Outstanding rule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOutstandingRule(int id, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.DeleteOutstandingRuleAsync(id, cancellationToken);
            return Json(new { ok = true, message = "Outstanding rule deleted." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Outstanding rule not found." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotificationSettings(NotificationSetting setting, string smtpPassword, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveNotificationSettingsAsync(setting, smtpPassword, cancellationToken);
            return Json(new { ok = true, message = "Notification settings saved successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TestSmtpSettings(string host, int port, bool useSsl, string user, string password, string fromEmail, string fromName, string testEmail, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(testEmail))
                return BadRequest(new { message = "Recipient test email address is required." });

            var ok = await controlTowerService.TestSmtpSettingsAsync(host, port, useSsl, user, password, fromEmail, fromName, testEmail, cancellationToken);
            if (ok)
            {
                return Json(new { ok = true, message = "SMTP test mail sent successfully!" });
            }
            return BadRequest(new { message = "SMTP connection or authentication failed. Check host/port/creds." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> TestSmsSettings(string apiKey, string senderId, string testMobile, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(testMobile))
                return BadRequest(new { message = "Recipient test mobile number is required." });

            var ok = await controlTowerService.TestSmsSettingsAsync(apiKey, senderId, testMobile, cancellationToken);
            if (ok)
            {
                return Json(new { ok = true, message = "MSG91 test SMS sent successfully!" });
            }
            return BadRequest(new { message = "SMS send failed. Check authkey or API status." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRolePermissions(List<RolePermissionInput> permissions, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveRolePermissionsAsync(permissions, cancellationToken);

            // Bust the layout cache token as well
            var old = Interlocked.Exchange(ref RolePermEvictionSource, new CancellationTokenSource());
            old.Cancel();
            old.Dispose();

            return Json(new { ok = true, message = "Role permissions matrix saved successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTallySettings(TallyIntegrationSetting setting, string tallyPassword, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveTallySettingsAsync(setting, tallyPassword, cancellationToken);
            return Json(new { ok = true, message = "Tally integration settings saved." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogDiff(long id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await controlTowerService.GetAuditLogDiffAsync(id, cancellationToken);
            return Json(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHelpText(HelpText help, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveHelpTextAsync(help, cancellationToken);
            return Json(new { ok = true, message = "Help text block updated successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReportColumnConfig(ReportColumnConfig config, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SaveReportColumnConfigAsync(config, cancellationToken);
            return Json(new { ok = true, message = "Report column visibility config saved successfully." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReportColumnConfig(int id, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.DeleteReportColumnConfigAsync(id, cancellationToken);
            return Json(new { ok = true, message = "Report column rule removed." });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // FORMULA BUILDER PAGE
    // =========================================================
    public async Task<IActionResult> FormulaBuilder(CancellationToken cancellationToken)
    {
        ViewBag.SeededVariables = FormulaConstants.AllVariables;
        ViewBag.ActiveFormula = await db.PortalSettings
            .Where(s => s.Key == "IncentiveFormula" && !s.IsDeleted)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken) ?? "";
        return View();
    }

    // =========================================================
    // ALTERNATE PARTY CODE MAPPINGS
    // =========================================================
    public async Task<IActionResult> PartyCodeMappings(string? search, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var (mappings, totalPages) = await controlTowerService.GetPartyCodeMappingsAsync(search, page, pageSize, cancellationToken);
        
        ViewBag.Search = search;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.PageSize = pageSize;

        return View(mappings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePartyCodeMapping(int id, string alternativeCode, string originalCode, string? notes, bool isActive, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.SavePartyCodeMappingAsync(id, alternativeCode, originalCode, notes, isActive, cancellationToken);
            TempData["Success"] = id == 0 ? "Mapping created successfully." : "Mapping updated successfully.";
        }
        catch (KeyNotFoundException)
        {
            TempData["Error"] = "Mapping not found.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(PartyCodeMappings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePartyCodeMapping(int id, CancellationToken cancellationToken)
    {
        try
        {
            await controlTowerService.DeletePartyCodeMappingAsync(id, cancellationToken);
            TempData["Success"] = "Mapping deleted successfully.";
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(PartyCodeMappings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportPartyCodeMappings(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a valid Excel file.";
            return RedirectToAction(nameof(PartyCodeMappings));
        }

        try
        {
            var (importedCount, skippedCount) = await controlTowerService.ImportPartyCodeMappingsAsync(file, cancellationToken);
            TempData["Success"] = $"Successfully processed {importedCount} mappings. (Skipped {skippedCount} empty rows)";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Import failed: {ex.Message}";
        }

        return RedirectToAction(nameof(PartyCodeMappings));
    }
}

public static class FormulaConstants
{
    public static readonly string[] AllVariables =
    [
        "SaleValue", "AchievementPercent", "SlabRate", "TdsRate",
        "OnBillDiscount", "GrossIncentive", "NetTransferAmount",
        "Sales", "Growth", "Outstanding", "CollectionPercent",
        "Discount", "DealerCategory", "ProductGroup", "Branch",
        "Region", "State", "OutstandingPercent", "GrowthPercent",
        "TargetAchievement"
    ];
}
