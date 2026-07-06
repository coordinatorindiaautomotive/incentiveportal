using System.Globalization;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize]
public class ExternalIncentiveController(IncentiveDbContext db, ICurrentUser cu) : Controller
{
    // Safely read any cell as plain string — handles ALL XLDataType variants
    // without ever calling GetString() / Convert.ToString() (which throws on formula errors).
    private static string CellText(IXLCell cell)
    {
        try
        {
            if (cell == null || cell.IsEmpty()) return "";
            var val = cell.Value;
            return val.Type switch
            {
                XLDataType.Number   => val.GetNumber().ToString(CultureInfo.InvariantCulture),
                XLDataType.Text     => val.GetText() ?? "",
                XLDataType.Boolean  => val.GetBoolean() ? "TRUE" : "FALSE",
                XLDataType.DateTime => val.GetDateTime().ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                XLDataType.TimeSpan => val.GetTimeSpan().ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
                XLDataType.Blank    => "",
                XLDataType.Error    => "",
                _                   => ""
            };
        }
        catch { return ""; }
    }

    // ── Access helpers (mirror CashManagementController pattern) ───────
    private bool IsHO() =>
        cu.IsInRole(AppRoles.SuperAdmin) ||
        cu.IsInRole(AppRoles.HOFinance)  ||
        cu.IsInRole(AppRoles.Auditor);

    /// Returns the current user's branch Code (e.g. "GRL", "ALW") or null for HO/Admin.
    private async Task<string?> GetMyBranchCodeAsync() =>
        (!IsHO() && cu.BranchId.HasValue)
            ? await db.Branches
                  .Where(b => b.Id == cu.BranchId.Value && !b.IsDeleted)
                  .Select(b => b.Code)
                  .FirstOrDefaultAsync()
            : null;

    // ────────────────────────────────────────────────────────────────
    // GET  /ExternalIncentive
    // ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index(int? month, int? year, string? partyCode, string? location)
    {
        var now      = DateTime.Now;
        var selMonth = month ?? now.Month;
        var selYear  = year  ?? now.Year;

        // ── Branch isolation: non-HO users only see their own branch ──
        var myBranchCode = await GetMyBranchCodeAsync();
        // If user is branch-scoped, lock the location filter to their branch code
        if (myBranchCode != null)
            location = myBranchCode;

        var query = db.ExternalIncentiveRecords
            .Include(r => r.Upload)
            .Where(r => r.Month == selMonth && r.Year == selYear && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(r => r.Location == location);
        if (!string.IsNullOrWhiteSpace(partyCode))
            query = query.Where(r => r.ConsPartyCode.Contains(partyCode) || r.ConsPartyName.Contains(partyCode));

        var records = await query
            .OrderBy(r => r.Location).ThenBy(r => r.ConsPartyName)
            .ToListAsync();

        // Location dropdown: HO sees all; branch users see only their location
        List<string> locations;
        if (myBranchCode != null)
        {
            locations = new List<string> { myBranchCode };
        }
        else
        {
            locations = await db.ExternalIncentiveRecords
                .Where(r => r.Month == selMonth && r.Year == selYear && !r.IsDeleted)
                .Select(r => r.Location).Distinct().OrderBy(l => l)
                .ToListAsync();
        }

        var uploadsQuery = db.ExternalIncentiveUploads
            .Where(u => u.Month == selMonth && u.Year == selYear && !u.IsDeleted);
        var uploads = await uploadsQuery.OrderByDescending(u => u.CreatedAt).ToListAsync();

        ViewBag.SelMonth       = selMonth;
        ViewBag.SelYear        = selYear;
        ViewBag.PartyCode      = partyCode;
        ViewBag.Location       = location;
        ViewBag.Locations      = locations;
        ViewBag.Uploads        = uploads;
        ViewBag.MyBranchCode   = myBranchCode;   // tells view to lock the location dropdown
        ViewBag.TotalNetSale   = records.Sum(r => r.NetRetailSelling);
        ViewBag.TotalDiscount  = records.Sum(r => r.DiscountAmount);
        ViewBag.TotalIncentive = records.Sum(r => r.Incentive);

        return View(records);
    }

    // ────────────────────────────────────────────────────────────────
    // GET  /ExternalIncentive/Upload
    // ────────────────────────────────────────────────────────────────
    [Authorize(Roles = "Super Admin,HO Finance")]
    public IActionResult Upload() => View();

    // ────────────────────────────────────────────────────────────────
    // POST /ExternalIncentive/Upload
    // ────────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> Upload(IFormFile file, int month, int year, string? remarks, bool replaceExisting = false)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Please select a file before uploading.";
            return RedirectToAction(nameof(Upload));
        }

        var ext = Path.GetExtension(file.FileName).ToLower();
        if (ext != ".xlsx" && ext != ".xls")
        {
            TempData["Error"] = "Only Excel files (.xlsx / .xls) are supported.";
            return RedirectToAction(nameof(Upload));
        }

        var period = await db.IncentivePeriods
            .FirstOrDefaultAsync(p => p.Year == year && p.Month == month && !p.IsDeleted);

        if (period != null)
        {
            if (period.LockedFlag)
            {
                TempData["Error"] = $"The period {month:D2}/{year} is locked and cannot be modified.";
                return RedirectToAction(nameof(Upload));
            }
            if (period.SourceType != "PayoutImport")
            {
                TempData["Error"] = $"The period {month:D2}/{year} is already owned by source type '{period.SourceType}'. Mixed processing is not allowed.";
                return RedirectToAction(nameof(Upload));
            }
        }


        if (replaceExisting)
        {
            var old = await db.ExternalIncentiveRecords
                .Where(r => r.Month == month && r.Year == year)
                .ToListAsync();
            foreach (var r in old) r.IsDeleted = true;

            var oldUploads = await db.ExternalIncentiveUploads
                .Where(u => u.Month == month && u.Year == year)
                .ToListAsync();
            foreach (var u in oldUploads) u.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        var monthLabel = new DateTime(year, month, 1).ToString("MMM-yy", CultureInfo.InvariantCulture);
        var records    = new List<ExternalIncentiveRecord>();

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        try
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws == null || ws.LastRowUsed() == null)
            {
                TempData["Error"] = "No worksheet found in the uploaded Excel file.";
                return RedirectToAction(nameof(Upload));
            }

            int maxRow = ws.LastRowUsed()!.RowNumber();
            int maxCol = ws.LastColumnUsed()?.ColumnNumber() ?? 20;

            // Auto-detect header row (scan first 10 rows)
            int headerRow = 1;
            int colMonth = 0, colCode = 0, colName = 0, colLoc = 0,
                colNet   = 0, colDisc = 0, colSlab = 0, colIncentive = 0;

            for (int r = 1; r <= Math.Min(10, maxRow); r++)
            {
                bool found = false;
                for (int c = 1; c <= maxCol; c++)
                {
                    var h = CellText(ws.Cell(r, c)).Trim().ToLowerInvariant();
                    if (h.Contains("month"))                                           { colMonth    = c; headerRow = r; found = true; }
                    if (h.Contains("party code") || h.Contains("partycode"))           { colCode     = c; found = true; }
                    if (h.Contains("party name") || h.Contains("partyname"))           { colName     = c; }
                    if (h is "location" or "loc" or "branch" || h.Contains("locat"))  { colLoc      = c; }
                    if (h.Contains("net retail") || h.Contains("net sale") || h.Contains("selling")) { colNet = c; }
                    if (h.Contains("discount"))                                         colDisc      = c;
                    if (h is "slab" || h.StartsWith("slab"))                            colSlab      = c;
                    if (h is "incentive" || h.Contains("incentive amount"))             colIncentive = c;
                }
                if (found && colCode > 0) break;
            }

            if (colCode == 0 || colNet == 0)
            {
                TempData["Error"] = "Excel format is invalid. Columns 'Cons Party Code' and 'Net Retail Selling' are required.";
                return RedirectToAction(nameof(Upload));
            }

            for (int row = headerRow + 1; row <= maxRow; row++)
            {
                var code = CellText(ws.Cell(row, colCode)).Trim();
                if (string.IsNullOrWhiteSpace(code)) continue;

                // Read month label — handles text ("May-26"), DateTime cells, AND Excel OA date
                // serials stored as numbers (e.g. 46082 → "May-26")
                string rawLabel = monthLabel;
                if (colMonth > 0)
                {
                    var monthCell = ws.Cell(row, colMonth);
                    try
                    {
                        var mv = monthCell.Value;
                        if (mv.Type == XLDataType.DateTime)
                        {
                            rawLabel = mv.GetDateTime().ToString("MMM-yy", CultureInfo.InvariantCulture);
                        }
                        else if (mv.Type == XLDataType.Number)
                        {
                            // Excel stores dates as OA-date floats (e.g. 46082 = May 2026)
                            // Only treat as date if plausible Excel date range (>= 1 = 1900-01-01)
                            var num = mv.GetNumber();
                            if (num >= 1 && num < 2958466) // upper: Dec 31 9999 in OA
                            {
                                try { rawLabel = DateTime.FromOADate(num).ToString("MMM-yy", CultureInfo.InvariantCulture); }
                                catch { rawLabel = num.ToString(CultureInfo.InvariantCulture); }
                            }
                            else rawLabel = CellText(monthCell).Trim();
                        }
                        else
                        {
                            rawLabel = CellText(monthCell).Trim();
                        }
                    }
                    catch { rawLabel = monthLabel; }
                }
                if (string.IsNullOrWhiteSpace(rawLabel)) rawLabel = monthLabel;

                var name    = colName > 0 ? CellText(ws.Cell(row, colName)).Trim() : "";
                var loc     = colLoc  > 0 ? CellText(ws.Cell(row, colLoc)).Trim()  : "";
                var slabStr = colSlab > 0 ? CellText(ws.Cell(row, colSlab)).Trim() : "";

                decimal ParseCell(int col) =>
                    decimal.TryParse(
                        CellText(ws.Cell(row, col)).Replace(",", "").Trim(),
                        NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

                var net       = colNet      > 0 ? ParseCell(colNet)      : 0m;
                var disc      = colDisc     > 0 ? ParseCell(colDisc)     : 0m;
                var incentive = colIncentive > 0 ? ParseCell(colIncentive) : 0m;

                records.Add(new ExternalIncentiveRecord
                {
                    Month            = month,
                    Year             = year,
                    MonthLabel       = rawLabel,
                    ConsPartyCode    = code,
                    ConsPartyName    = name,
                    Location         = loc,
                    NetRetailSelling = net,
                    DiscountAmount   = disc,
                    Slab             = slabStr,
                    Incentive        = incentive
                });
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to read Excel file: {ex.Message}";
            return RedirectToAction(nameof(Upload));
        }

        if (records.Count == 0)
        {
            TempData["Error"] = "No valid data rows found in the Excel file.";
            return RedirectToAction(nameof(Upload));
        }

        var upload = new ExternalIncentiveUpload
        {
            FileName   = file.FileName,
            Month      = month,
            Year       = year,
            MonthLabel = monthLabel,
            TotalRows  = records.Count,
            Status     = "Completed",
            Remarks    = remarks
        };
        db.ExternalIncentiveUploads.Add(upload);

        if (period == null)
        {
            period = new IncentivePeriod
            {
                Year = year,
                Month = month,
                SourceType = "PayoutImport",
                Status = "Draft",
                LockedFlag = false
            };
            db.IncentivePeriods.Add(period);
        }
        else
        {
            period.SourceType = "PayoutImport";
            period.Status = "Draft";
            db.Entry(period).State = EntityState.Modified;
        }

        await db.SaveChangesAsync();

        foreach (var rec in records)
            rec.UploadId = upload.Id;

        db.ExternalIncentiveRecords.AddRange(records);
        await db.SaveChangesAsync();

        TempData["Success"] = $"{records.Count} records uploaded successfully for {monthLabel}.";
        return RedirectToAction(nameof(Index), new { month, year });
    }

    // ────────────────────────────────────────────────────────────────
    // POST /ExternalIncentive/DeleteUpload/{id}
    // ────────────────────────────────────────────────────────────────
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> DeleteUpload(int id)
    {
        var upload = await db.ExternalIncentiveUploads
            .Include(u => u.Records)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (upload == null) return NotFound();

        var period = await db.IncentivePeriods
            .FirstOrDefaultAsync(p => p.Year == upload.Year && p.Month == upload.Month && !p.IsDeleted);
        if (period != null && period.LockedFlag)
        {
            TempData["Error"] = $"The period {upload.Month:D2}/{upload.Year} is locked. Unlock it first.";
            return RedirectToAction(nameof(Index), new { upload.Month, upload.Year });
        }

        foreach (var r in upload.Records) r.IsDeleted = true;
        upload.IsDeleted = true;

        var otherActiveUploadsExist = await db.ExternalIncentiveUploads
            .AnyAsync(u => u.Month == upload.Month && u.Year == upload.Year && u.Id != upload.Id && !u.IsDeleted);
        if (!otherActiveUploadsExist && period != null)
        {
            period.IsDeleted = true;
            db.Entry(period).State = EntityState.Modified;
        }

        await db.SaveChangesAsync();

        TempData["Success"] = $"Upload '{upload.FileName}' has been deleted.";
        return RedirectToAction(nameof(Index), new { upload.Month, upload.Year });
    }

    // ────────────────────────────────────────────────────────────────
    // GET  /ExternalIncentive/ExportExcel
    // ────────────────────────────────────────────────────────────────
    public async Task<IActionResult> ExportExcel(int month, int year, string? location)
    {
        // Branch users can only export their own branch
        var myBranchCode = await GetMyBranchCodeAsync();
        if (myBranchCode != null) location = myBranchCode;

        var query = db.ExternalIncentiveRecords
            .Where(r => r.Month == month && r.Year == year && !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(r => r.Location == location);

        var records = await query
            .OrderBy(r => r.Location).ThenBy(r => r.ConsPartyName)
            .ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Incentive Register");

        var headers = new[] { "Month", "Cons Party Code", "Cons Party Name", "Location",
                               "Net Retail Selling", "Discount Amount", "Slab", "Incentive" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int row = 2;
        foreach (var r in records)
        {
            ws.Cell(row, 1).Value = r.MonthLabel;
            ws.Cell(row, 2).Value = r.ConsPartyCode;
            ws.Cell(row, 3).Value = r.ConsPartyName;
            ws.Cell(row, 4).Value = r.Location;
            ws.Cell(row, 5).Value = r.NetRetailSelling;
            ws.Cell(row, 6).Value = r.DiscountAmount;
            ws.Cell(row, 7).Value = r.Slab;
            ws.Cell(row, 8).Value = r.Incentive;
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
            row++;
        }

        // Totals
        ws.Cell(row, 4).Value = "TOTAL";
        ws.Cell(row, 5).Value = records.Sum(r => r.NetRetailSelling);
        ws.Cell(row, 6).Value = records.Sum(r => r.DiscountAmount);
        ws.Cell(row, 8).Value = records.Sum(r => r.Incentive);
        ws.Row(row).Style.Font.Bold = true;
        ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromArgb(240, 253, 244);

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var fname = $"ExternalIncentiveRegister_{year}_{month:D2}.xlsx";
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fname);
    }
}
