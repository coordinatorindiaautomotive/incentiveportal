using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;
using IncentivePortal.Services;
using IncentivePortal.Helpers;

namespace IncentivePortal.Controllers;

/// <summary>
/// Controller responsible for rendering incentive registers, outstanding masters, and performance metrics.
/// Delegates business logic and spreadsheet exports to specialized application services.
/// </summary>
[Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager,Sales Executive")]
public sealed class ReportsController(
    IncentiveDbContext db,
    ICurrentUser currentUser,
    ITallyIntegrationService tallyService,
    IReportBuilderService reportBuilder,
    IReportExportService reportExport,
    Microsoft.Extensions.Configuration.IConfiguration configuration) : Controller
{
    /// <summary>
    /// Renders the main Incentive Register table showing net transfer payouts.
    /// </summary>
    public async Task<IActionResult> IncentiveRegister([FromQuery] IncentiveRegisterFilter filter, CancellationToken cancellationToken)
        => View(await reportBuilder.BuildIncentiveRegisterAsync(filter, cancellationToken));

    /// <summary>
    /// Generates and streams a styled, formatted Excel spreadsheet containing the detailed Incentive Register.
    /// </summary>
    public async Task<IActionResult> DownloadIncentiveRegister([FromQuery] IncentiveRegisterFilter filter, CancellationToken cancellationToken)
    {
        var model = await reportBuilder.BuildIncentiveRegisterAsync(filter, cancellationToken);
        var fileBytes = await reportExport.ExportIncentiveRegisterAsync(model, cancellationToken);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Incentive_Register_{DateTime.Now:yyyyMMddHHmm}.xlsx");
    }

    /// <summary>
    /// Exports the 100% complete, raw transactional sales data exactly as stored in the SQL server.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> DownloadRawSales(int month, int year, CancellationToken cancellationToken)
    {
        var fileBytes = await reportExport.ExportRawSalesAsync(month, year, cancellationToken);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Raw_Sales_Data_{year}_{month:D2}_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Renders the Outstanding Adjustments Ledger, listing all in-memory deductions made against outstanding values.
    /// </summary>
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> OutstandingAdjustment(CancellationToken cancellationToken)
    {
        var query = db.SsIncentives.Where(x => !x.IsDeleted && (x.GrossIncentive - x.TdsAmount - x.NetTransferAmount) > 0m);
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            var branchCode = await db.Branches
                .Where(b => b.Id == currentUser.BranchId.Value)
                .Select(b => b.Code)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrEmpty(branchCode))
            {
                query = query.Where(x => x.SourceLocation == branchCode);
            }
        }

        var incentives = await query.ToListAsync(cancellationToken);
        var partyCodes = incentives.Select(x => x.PartyCode).Distinct().ToList();
        var outstandings = await db.DealerOutstandings
            .Where(o => !o.IsDeleted && partyCodes.Contains(o.PartyCode))
            .ToListAsync(cancellationToken);

        var result = incentives.Select(inc => {
            var outs = outstandings.FirstOrDefault(o => o.Year == inc.Year && o.Month == inc.Month && o.PartyCode == inc.PartyCode);
            return new OutstandingAdjustmentRow(
                inc.PartyCode,
                inc.PartyName,
                inc.GrossIncentive,
                inc.TdsAmount,
                inc.NetTransferAmount,
                outs?.Outstanding ?? 0m
            );
        }).ToList();

        return View(result);
    }

    /// <summary>
    /// Renders the Outstanding Balances Master sheet showing current balances, sales values, and sync timestamps.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> OutstandingMaster(int? month, int? year, CancellationToken cancellationToken)
    {
        if (!month.HasValue || !year.HasValue)
        {
            var latestPeriod = await db.SsIncentives
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .Select(s => new { s.Year, s.Month })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestPeriod != null)
            {
                month = latestPeriod.Month;
                year = latestPeriod.Year;
            }
            else
            {
                month = DateTime.Today.Month;
                year = DateTime.Today.Year;
            }
        }

        ViewBag.SelectedMonth = month.Value;
        ViewBag.SelectedYear = year.Value;
        ViewBag.TallyUrl = configuration["Tally:Url"] ?? "http://localhost:9000";

        var viewModel = await reportBuilder.GetOutstandingMasterAsync(month, year, cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Generates and streams a styled, formatted Excel spreadsheet containing the detailed Outstanding Master.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> DownloadOutstandingMaster(int? month, int? year, CancellationToken cancellationToken)
    {
        // Resolve month and year if null, matching OutstandingMaster action logic
        if (!month.HasValue || !year.HasValue)
        {
            var latestPeriod = await db.DealerOutstandings
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .OrderByDescending(s => s.Year)
                .ThenByDescending(s => s.Month)
                .Select(s => new { s.Year, s.Month })
                .FirstOrDefaultAsync(cancellationToken);

            if (latestPeriod != null)
            {
                month ??= latestPeriod.Month;
                year ??= latestPeriod.Year;
            }
            else
            {
                month ??= DateTime.Today.Month;
                year ??= DateTime.Today.Year;
            }
        }

        var model = await reportBuilder.GetOutstandingMasterAsync(month, year, cancellationToken);
        var fileBytes = await reportExport.ExportOutstandingMasterAsync(model, cancellationToken);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Outstanding_Master_{DateTime.Now:yyyyMMddHHmm}.xlsx");
    }

    /// <summary>
    /// Trigger endpoint to perform Tally ERP 9 outstanding balance synchronization.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SyncOutstandingFromTally(int? month, int? year, CancellationToken cancellationToken)
    {
        try
        {
            var syncLogs = await tallyService.SyncOutstandingAsync(month, year, cancellationToken);
            return Json(new { ok = true, message = "Successfully synchronized outstanding balances with Tally ERP 9.", logs = syncLogs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Sync failed: {ex.Message}" });
        }
    }

    /// <summary>
    /// Uploads an outstanding balance Excel statement to update master balances.
    /// </summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 262144000)]
    public async Task<IActionResult> UploadOutstandingExcel(IFormFile file, bool rewrite, int? month, int? year, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Please select a valid Excel file." });

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.First();

            // 1. Dynamic Header Row Scanning
            int headerRowIndex = 1;
            int codeCol = 0;
            int outstandingCol = 0;
            int colLess7 = 0;
            int col7To14 = 0;
            int col14To21 = 0;
            int col21To28 = 0;
            int col28To35 = 0;
            int col35To50 = 0;
            int col50To80 = 0;
            int colMore80 = 0;

            for (int r = 1; r <= 10; r++)
            {
                var row = sheet.Row(r);
                var rowHeaders = row.CellsUsed().ToDictionary(
                    c => c.Value.ToString().Trim(), 
                    c => c.Address.ColumnNumber, 
                    StringComparer.OrdinalIgnoreCase
                );

                // Find Code Column
                int cCol = 0;
                foreach (var key in new[] { "Particulars", "Party Code", "Dealer Code", "PartyCode", "DealerCode", "Code", "Ledger Name", "Ledger", "Name" })
                {
                    if (rowHeaders.ContainsKey(key))
                    {
                        cCol = rowHeaders[key];
                        break;
                    }
                }

                // Find Outstanding Column
                int oCol = 0;
                foreach (var key in new[] { "Pending Bills", "Outstanding", "Balance", "Closing Balance", "Outstanding Balance", "Amt", "Amount", "Closing" })
                {
                    if (rowHeaders.ContainsKey(key))
                    {
                        oCol = rowHeaders[key];
                        break;
                    }
                }

                if (cCol != 0 && oCol != 0)
                {
                    headerRowIndex = r;
                    codeCol = cCol;
                    outstandingCol = oCol;

                    // Now scan for the aging columns in this same header row
                    foreach (var kv in rowHeaders)
                    {
                        var key = kv.Key;
                        var colIndex = kv.Value;
                        string normKey = key.Replace(" ", "").Replace("(", "").Replace(")", "").ToLower();
                        
                        if (normKey.Contains("<7days") || normKey.Contains("less7days") || normKey == "<7") colLess7 = colIndex;
                        else if (normKey.Contains("7to14") || normKey.Contains("7-14")) col7To14 = colIndex;
                        else if (normKey.Contains("14to21") || normKey.Contains("14-21")) col14To21 = colIndex;
                        else if (normKey.Contains("21to28") || normKey.Contains("21-28")) col21To28 = colIndex;
                        else if (normKey.Contains("28to35") || normKey.Contains("28-35")) col28To35 = colIndex;
                        else if (normKey.Contains("35to50") || normKey.Contains("35-50")) col35To50 = colIndex;
                        else if (normKey.Contains("50to80") || normKey.Contains("50-80")) col50To80 = colIndex;
                        else if (normKey.Contains(">80days") || normKey.Contains("more80days") || normKey == ">80") colMore80 = colIndex;
                    }
                    break;
                }
            }

            if (codeCol == 0 || outstandingCol == 0)
            {
                return BadRequest(new { message = "Invalid file structure. Excel sheet must contain a Code/Name column and an Outstanding/Balance column in the first 10 rows." });
            }

            var parties = await db.Parties.Include(p => p.Branch).Where(p => !p.IsDeleted).ToListAsync(cancellationToken);

            // Helper to parse decimal cell
            decimal? ParseCellDecimal(IXLCell cell)
            {
                if (cell == null || cell.IsEmpty()) return null;
                
                if (cell.Value.IsNumber)
                {
                    return (decimal)cell.Value.GetNumber();
                }
                
                var str = cell.Value.ToString().Trim();
                if (string.IsNullOrEmpty(str)) return null;
                
                if (str.StartsWith("="))
                {
                    if (cell.CachedValue.IsNumber)
                    {
                        return (decimal)cell.CachedValue.GetNumber();
                    }
                    str = cell.CachedValue.ToString().Trim();
                }
                
                if (string.IsNullOrEmpty(str)) return null;
                
                bool isCredit = str.Contains("Cr", StringComparison.OrdinalIgnoreCase) || str.Contains("cr", StringComparison.OrdinalIgnoreCase) || str.StartsWith("-");
                var cleanStr = System.Text.RegularExpressions.Regex.Replace(str, @"[^\d.-]", "");
                if (decimal.TryParse(cleanStr, out var val))
                {
                    return isCredit ? -Math.Abs(val) : val;
                }
                return null;
            }

            // Helper to merge decimal values
            decimal? MergeDecimal(decimal? existing, decimal? parsed)
            {
                if (existing == null && parsed == null) return null;
                return (existing ?? 0m) + (parsed ?? 0m);
            }

            // 2. Decoupled Dealer Outstanding Registry
            int targetMonth = month ?? DateTime.UtcNow.Month;
            int targetYear = year ?? DateTime.UtcNow.Year;
            string monthLabel = new DateTime(targetYear, targetMonth, 1).ToString("MMMM yyyy");

            if (rewrite)
            {
                var existingOuts = await db.DealerOutstandings
                    .Where(o => o.Year == targetYear && o.Month == targetMonth && !o.IsDeleted)
                    .ToListAsync(cancellationToken);
                db.DealerOutstandings.RemoveRange(existingOuts);
                await db.SaveChangesAsync(cancellationToken);
            }

            var activeOutstandings = new Dictionary<string, DealerOutstanding>(StringComparer.OrdinalIgnoreCase);
            if (!rewrite)
            {
                var existingOuts = await db.DealerOutstandings
                    .Where(o => o.Year == targetYear && o.Month == targetMonth && !o.IsDeleted)
                    .ToListAsync(cancellationToken);
                foreach (var o in existingOuts)
                {
                    activeOutstandings[o.PartyCode] = o;
                }
            }

            var currentSales = await db.SsIncentives
                .Where(s => s.Year == targetYear && s.Month == targetMonth && !s.IsDeleted)
                .ToDictionaryAsync(s => s.PartyCode, s => s, cancellationToken);

            int updatedCount = 0;
            var uploadLogs = new List<string>();
            var rows = sheet.RowsUsed().Skip(headerRowIndex).ToList();

            uploadLogs.Add($"[Excel] Resolved Header on Row {headerRowIndex}. Code Column = Col {codeCol}, Outstanding Column = Col {outstandingCol}. Mode: {(rewrite ? "Overwrite/Rewrite" : "Accumulate/Merge")}");

            foreach (var row in rows)
            {
                var codeVal = row.Cell(codeCol).Value.ToString().Trim();
                if (string.IsNullOrEmpty(codeVal)) continue;
                if (codeVal.Equals("Total", StringComparison.OrdinalIgnoreCase) || codeVal.Equals("Grand Total", StringComparison.OrdinalIgnoreCase)) continue;

                string matchCode = codeVal;
                string matchName = codeVal;
                var parenMatch = System.Text.RegularExpressions.Regex.Match(codeVal, @"\(([^)]+)\)");
                if (parenMatch.Success)
                {
                    matchCode = parenMatch.Groups[1].Value.Trim();
                    matchName = codeVal.Substring(0, parenMatch.Index).Trim();
                }

                // Prioritize exact PartyCode match
                var party = parties.FirstOrDefault(p => p.PartyCode.Equals(matchCode, StringComparison.OrdinalIgnoreCase));

                // If not matched, try exact PartyName match
                if (party == null && !string.IsNullOrEmpty(matchName))
                {
                    party = parties.FirstOrDefault(p => p.PartyName.Equals(matchName, StringComparison.OrdinalIgnoreCase));
                }

                // If not matched, try flipped matches (exact only)
                if (party == null)
                {
                    party = parties.FirstOrDefault(p => 
                        p.PartyCode.Equals(matchName, StringComparison.OrdinalIgnoreCase) ||
                        p.PartyName.Equals(matchCode, StringComparison.OrdinalIgnoreCase));
                }

                if (party == null)
                {
                    var truncatedCode = matchCode.Substring(0, Math.Min(40, matchCode.Length));
                    int dynamicBranchId = 1;
                    if (matchCode.Contains("RJ06", StringComparison.OrdinalIgnoreCase) || matchName.Contains("RJ06", StringComparison.OrdinalIgnoreCase))
                    {
                        dynamicBranchId = 37; // Jaipur / TRANSPORT NAGAR-SPR
                    }
                    else if (matchCode.Contains("RJ05", StringComparison.OrdinalIgnoreCase) || matchName.Contains("RJ05", StringComparison.OrdinalIgnoreCase))
                    {
                        dynamicBranchId = 5; // Alwar / ALWAR-SPR
                    }

                    party = new Party
                    {
                        PartyCode = truncatedCode,
                        PartyName = matchName,
                        GST = "",
                        Mobile = "",
                        Address = "Created dynamically from Excel Outstanding upload",
                        DealerType = "INDEPENDENT WORKSHOP",
                        FixedIncentivePercent = 0m,
                        BranchId = dynamicBranchId,
                        Status = "Active"
                    };
                    db.Parties.Add(party);
                    await db.SaveChangesAsync(cancellationToken);

                    parties.Add(party);
                    uploadLogs.Add($"[Excel-Create] Dynamic Dealer Registered: Code '{truncatedCode}', Name '{matchName}' under BranchId {dynamicBranchId}");
                }

                decimal parsedOutstanding = ParseCellDecimal(row.Cell(outstandingCol)) ?? 0m;
                decimal? parsedLess7 = colLess7 > 0 ? ParseCellDecimal(row.Cell(colLess7)) : null;
                decimal? parsed7To14 = col7To14 > 0 ? ParseCellDecimal(row.Cell(col7To14)) : null;
                decimal? parsed14To21 = col14To21 > 0 ? ParseCellDecimal(row.Cell(col14To21)) : null;
                decimal? parsed21To28 = col21To28 > 0 ? ParseCellDecimal(row.Cell(col21To28)) : null;
                decimal? parsed28To35 = col28To35 > 0 ? ParseCellDecimal(row.Cell(col28To35)) : null;
                decimal? parsed35To50 = col35To50 > 0 ? ParseCellDecimal(row.Cell(col35To50)) : null;
                decimal? parsed50To80 = col50To80 > 0 ? ParseCellDecimal(row.Cell(col50To80)) : null;
                decimal? parsedMore80 = colMore80 > 0 ? ParseCellDecimal(row.Cell(colMore80)) : null;

                if (activeOutstandings.TryGetValue(party.PartyCode, out var outEntry))
                {
                    outEntry.Outstanding += parsedOutstanding;
                    outEntry.OutstandingLess7Days = MergeDecimal(outEntry.OutstandingLess7Days, parsedLess7);
                    outEntry.Outstanding7To14Days = MergeDecimal(outEntry.Outstanding7To14Days, parsed7To14);
                    outEntry.Outstanding14To21Days = MergeDecimal(outEntry.Outstanding14To21Days, parsed14To21);
                    outEntry.Outstanding21To28Days = MergeDecimal(outEntry.Outstanding21To28Days, parsed21To28);
                    outEntry.Outstanding28To35Days = MergeDecimal(outEntry.Outstanding28To35Days, parsed28To35);
                    outEntry.Outstanding35To50Days = MergeDecimal(outEntry.Outstanding35To50Days, parsed35To50);
                    outEntry.Outstanding50To80Days = MergeDecimal(outEntry.Outstanding50To80Days, parsed50To80);
                    outEntry.OutstandingMore80Days = MergeDecimal(outEntry.OutstandingMore80Days, parsedMore80);
                    outEntry.UpdatedAt = DateTime.UtcNow;
                    outEntry.UpdatedBy = currentUser.UserName ?? "system";
                    
                    updatedCount++;
                    uploadLogs.Add($"[Excel] Dealer: {party.PartyCode} | Added: ₹{parsedOutstanding:N0} | Total: ₹{outEntry.Outstanding:N0}");
                }
                else
                {
                    var newOut = new DealerOutstanding
                    {
                        Month = targetMonth,
                        Year = targetYear,
                        MonthLabel = monthLabel,
                        PartyCode = party.PartyCode,
                        PartyName = party.PartyName,
                        Outstanding = parsedOutstanding,
                        OutstandingLess7Days = parsedLess7,
                        Outstanding7To14Days = parsed7To14,
                        Outstanding14To21Days = parsed14To21,
                        Outstanding21To28Days = parsed21To28,
                        Outstanding28To35Days = parsed28To35,
                        Outstanding35To50Days = parsed35To50,
                        Outstanding50To80Days = parsed50To80,
                        OutstandingMore80Days = parsedMore80,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = currentUser.UserName ?? "system"
                    };
                    db.DealerOutstandings.Add(newOut);
                    activeOutstandings[party.PartyCode] = newOut;
                    
                    updatedCount++;
                    uploadLogs.Add($"[Excel] Dealer: {party.PartyCode} | Set Outstanding: ₹{parsedOutstanding:N0}");
                }

                party.UpdatedAt = DateTime.UtcNow;
            }

            // Sync payment status on matching incentives
            foreach (var kvp in currentSales)
            {
                var partyCode = kvp.Key;
                var sale = kvp.Value;
                activeOutstandings.TryGetValue(partyCode, out var outEntry);
                decimal outstanding = outEntry?.Outstanding ?? 0m;
                
                if (outstanding != 0m)
                {
                    if (sale.PaymentStatus == "Pending" || sale.PaymentStatus == "Failed" || sale.PaymentStatus == "Reversed")
                    {
                        sale.PaymentStatus = "Credit Party";
                        db.Entry(sale).State = EntityState.Modified;
                    }
                }
                else
                {
                    if (sale.PaymentStatus == "Credit Party")
                    {
                        sale.PaymentStatus = "Pending";
                        db.Entry(sale).State = EntityState.Modified;
                    }
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            uploadLogs.Add($"Successfully processed Excel upload. Updated outstanding for {updatedCount} dealers.");

            return Json(new { ok = true, message = $"Successfully imported outstanding balances from Excel. Updated: {updatedCount}.", logs = uploadLogs });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Renders the list of pending dealer bank detail approval requests.
    /// </summary>
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> PendingApprovals(CancellationToken cancellationToken)
    {
        var query = db.BankApprovalRequests.Include(x => x.Party).Where(x => x.Status == "Pending").AsQueryable();
        if (currentUser.BranchId.HasValue && !currentUser.IsInRole(AppRoles.SuperAdmin) && !currentUser.IsInRole(AppRoles.HOFinance) && !currentUser.IsInRole(AppRoles.Auditor))
        {
            query = query.Where(x => x.Party.BranchId == currentUser.BranchId.Value);
        }
        return View(await query.ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Compiles and renders the multi-dimensional Performance Reports dashboard.
    /// </summary>
    public async Task<IActionResult> PerformanceReports(
        List<string>? dealerType,
        List<int>? branchId,
        List<string>? partyCode,
        List<string>? categories,
        int? month,
        int? year,
        CancellationToken cancellationToken)
    {
        var model = await reportBuilder.BuildPerformanceReportsAsync(dealerType, branchId, partyCode, categories, month, year, cancellationToken);
        return View(model);
    }

    /// <summary>
    /// Paginated AJAX data-provider for individual dealer performance reports.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDealerSales(
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
        var result = await reportBuilder.GetDealerSalesAsync(targetYear, quarters, months, partyTypes, categories, locations, dealerSubTypes, search, limit, cancellationToken);
        return Json(result);
    }

    /// <summary>
    /// Renders the Target vs Achievement comparison report for dealers.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> TargetVsAchievement(int? month, int? year, string? branchName, string? partyType, string? partCategoryCode, CancellationToken cancellationToken)
    {
        if (!month.HasValue || !year.HasValue)
        {
            month = 7;
            year = 2026;
        }

        ViewBag.SelectedMonth = month.Value;
        ViewBag.SelectedYear = year.Value;
        ViewBag.SelectedBranch = branchName;
        ViewBag.SelectedPartyType = partyType;
        ViewBag.SelectedPartCategory = partCategoryCode;

        var viewModel = await reportBuilder.GetTargetVsAchievementAsync(month, year, branchName, partyType, partCategoryCode, cancellationToken);
        return View(viewModel);
    }

    /// <summary>
    /// Generates and streams a styled, formatted Excel spreadsheet containing the Target vs Achievement report.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Super Admin,HO Finance,Auditor,Branch Manager")]
    public async Task<IActionResult> DownloadTargetVsAchievement(int? month, int? year, string? branchName, string? partyType, string? partCategoryCode, CancellationToken cancellationToken)
    {
        var model = await reportBuilder.GetTargetVsAchievementAsync(month, year, branchName, partyType, partCategoryCode, cancellationToken);
        var fileBytes = await reportExport.ExportTargetVsAchievementAsync(model, cancellationToken);
        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Target_Vs_Achievement_{year}_{month:D2}_{DateTime.Now:yyyyMMddHHmm}.xlsx");
    }

    /// <summary>
    /// Saves a single dealer target override from the inline editor.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SaveSingleTarget(string partyCode, int month, int year, decimal? targetValue, CancellationToken cancellationToken)
    {
        var target = await db.DealerTargets
            .FirstOrDefaultAsync(t => t.PartyCode == partyCode && t.Month == month && t.Year == year && !t.IsDeleted, cancellationToken);

        if (target == null)
        {
            target = new DealerTarget
            {
                Month = month,
                Year = year,
                PartyCode = partyCode,
                SystemSuggestedTarget = 0m
            };
            db.DealerTargets.Add(target);
        }

        target.AdminDefinedTarget = targetValue;
        target.FinalTarget = targetValue ?? target.SystemSuggestedTarget;
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedBy = User.Identity?.Name ?? "admin";

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true, finalTarget = target.FinalTarget });
    }

    /// <summary>
    /// Processes bulk target overrides (percentage shift or CSV data import).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> SaveBulkTargets(int month, int year, string adjustmentType, decimal? value, string? csvData, CancellationToken cancellationToken)
    {
        if (adjustmentType == "percentage" && value.HasValue)
        {
            var multiplier = 1m + (value.Value / 100m);
            var activeParties = await db.Parties.AsNoTracking().Where(p => !p.IsDeleted).Select(p => p.PartyCode).ToListAsync(cancellationToken);
            var existingTargets = await db.DealerTargets
                .Where(t => t.Month == month && t.Year == year && !t.IsDeleted && activeParties.Contains(t.PartyCode))
                .ToDictionaryAsync(t => t.PartyCode, t => t, cancellationToken);

            foreach (var code in activeParties)
            {
                if (!existingTargets.TryGetValue(code, out var target))
                {
                    target = new DealerTarget
                    {
                        Month = month,
                        Year = year,
                        PartyCode = code,
                        SystemSuggestedTarget = 0m
                    };
                    db.DealerTargets.Add(target);
                }

                var baseVal = target.AdminDefinedTarget ?? target.SystemSuggestedTarget;
                if (baseVal == 0m)
                {
                    var lmMonth = month - 1;
                    var lmYear = year;
                    if (lmMonth == 0) { lmMonth = 12; lmYear--; }
                    // N+1 here is acceptable for this edge case of 0 baseVal or we can just leave it as it was
                    var priorSales = await db.SsIncentives.AsNoTracking()
                        .Where(s => s.PartyCode == code && s.Month == lmMonth && s.Year == lmYear && !s.IsDeleted)
                        .Select(s => s.SaleValue)
                        .FirstOrDefaultAsync(cancellationToken);
                    baseVal = priorSales > 0 ? priorSales : 10000m;
                }
                
                target.AdminDefinedTarget = Math.Round(baseVal * multiplier, 0);
                target.FinalTarget = target.AdminDefinedTarget.Value;
                target.UpdatedAt = DateTime.UtcNow;
                target.UpdatedBy = User.Identity?.Name ?? "system";
            }
        }
        else if (adjustmentType == "csv" && !string.IsNullOrEmpty(csvData))
        {
            var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var parsedLines = lines.Select(l => l.Split(',', StringSplitOptions.TrimEntries)).Where(p => p.Length >= 2 && decimal.TryParse(p[1], out _)).ToList();
            var partyCodes = parsedLines.Select(p => p[0]).Distinct().ToList();
            var existingTargets = await db.DealerTargets
                .Where(t => t.Month == month && t.Year == year && !t.IsDeleted && partyCodes.Contains(t.PartyCode))
                .ToDictionaryAsync(t => t.PartyCode, t => t, cancellationToken);

            foreach (var parts in parsedLines)
            {
                var code = parts[0];
                var targetVal = decimal.Parse(parts[1]);

                if (!existingTargets.TryGetValue(code, out var target))
                {
                    target = new DealerTarget
                    {
                        Month = month,
                        Year = year,
                        PartyCode = code,
                        SystemSuggestedTarget = targetVal
                    };
                    db.DealerTargets.Add(target);
                }

                target.AdminDefinedTarget = targetVal;
                target.FinalTarget = targetVal;
                target.UpdatedAt = DateTime.UtcNow;
                target.UpdatedBy = User.Identity?.Name ?? "system";
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Json(new { ok = true });
    }
}
