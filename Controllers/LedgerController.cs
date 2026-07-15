using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class LedgerController(IncentiveDbContext db, IncentivePortal.Helpers.ICurrentUser currentUser) : Controller
{
    // =========================================================
    // INDEX PAGE
    // =========================================================
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var periods = await db.SsIncentives
            .AsNoTracking()
            .Select(x => new { x.Year, x.Month })
            .Distinct()
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        var monthNames = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        ViewBag.Periods = periods.Select(p => new {
            Year = p.Year,
            Month = p.Month,
            Label = p.Month > 0 && p.Month <= 12 ? $"{monthNames[p.Month - 1]} {p.Year}" : $"FY {p.Year}"
        }).ToList();

        return View();
    }

    // =========================================================
    // HIGH-PERFORMANCE SERVER-SIDE DATATABLES API
    // =========================================================
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> GetLedgerData(CancellationToken cancellationToken)
    {
        try
        {
            var form = Request.Form;
            int draw = int.TryParse(form["draw"], out var d) ? d : 1;
            int start = int.TryParse(form["start"], out var s) ? s : 0;
            int length = int.TryParse(form["length"], out var l) ? l : 10;
            string searchValue = form["search[value]"].ToString().Trim();

            // Filters
            string filterPeriod = form["filterPeriod"].ToString(); // "month-year" e.g., "5-2026"
            string filterStatus = form["filterStatus"].ToString(); // "Pending"/"Paid"/"Hold"

            var baseQuery = from inc in db.SsIncentives.AsNoTracking()
                            join p in db.Parties.AsNoTracking() on inc.PartyCode equals p.PartyCode into pGroup
                            from p in pGroup.DefaultIfEmpty()
                            join b in db.Branches.AsNoTracking() on (p != null ? p.BranchId : 0) equals b.Id into bGroup
                            from b in bGroup.DefaultIfEmpty()
                            join bd in db.BankDetails.AsNoTracking().Where(bd => bd.ApprovalStatus == "Approved" && !bd.IsDeleted) 
                                on (p != null ? p.Id : 0) equals bd.PartyId into bdGroup
                            from bd in bdGroup.DefaultIfEmpty()
                            where !inc.IsDeleted && inc.Status == "Posted"
                            select new { s = inc, p, b, bd };

            // Sales Executive isolation filter
            if (currentUser.IsInRole(AppRoles.SalesExecutive))
            {
                var mappedPartyCodes = await db.PartyExecutiveMappings
                    .AsNoTracking()
                    .Where(x => x.ExecutiveCode == currentUser.UserName)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);
                baseQuery = baseQuery.Where(x => mappedPartyCodes.Contains(x.s.PartyCode));
            }


            // Period filter
            if (!string.IsNullOrEmpty(filterPeriod) && filterPeriod != "all")
            {
                var parts = filterPeriod.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var y))
                {
                    baseQuery = baseQuery.Where(x => x.s.Month == m && x.s.Year == y);
                }
            }

            // Status filter
            if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "all")
            {
                baseQuery = baseQuery.Where(x => x.s.PaymentStatus == filterStatus);
            }

            // Search filter
            if (!string.IsNullOrEmpty(searchValue))
            {
                baseQuery = baseQuery.Where(x => x.s.PartyCode.Contains(searchValue) || 
                                                 x.s.PartyName.Contains(searchValue) || 
                                                 (x.b != null && x.b.Incharge.Contains(searchValue)) || 
                                                 (x.b != null && x.b.Code.Contains(searchValue)));
            }

            int recordsTotal;
            if (currentUser.IsInRole(AppRoles.SalesExecutive))
            {
                var mappedPartyCodes = await db.PartyExecutiveMappings
                    .AsNoTracking()
                    .Where(x => x.ExecutiveCode == currentUser.UserName)
                    .Select(x => x.PartyCode)
                    .ToListAsync(cancellationToken);
                recordsTotal = await db.SsIncentives.CountAsync(x => !x.IsDeleted && x.Status == "Posted" && mappedPartyCodes.Contains(x.PartyCode), cancellationToken);
            }

            else
            {
                recordsTotal = await db.SsIncentives.CountAsync(x => !x.IsDeleted && x.Status == "Posted", cancellationToken);
            }
            int recordsFiltered = await baseQuery.CountAsync(cancellationToken);

            // Sorting
            string sortColumnIndex = form["order[0][column]"].ToString();
            string sortDirection = form["order[0][dir]"].ToString(); // "asc" or "desc"
            string sortColumn = "Id";

            if (sortColumnIndex == "1") sortColumn = "Year";
            else if (sortColumnIndex == "2") sortColumn = "PartyCode";
            else if (sortColumnIndex == "3") sortColumn = "SaleValue";
            else if (sortColumnIndex == "4") sortColumn = "AchievementPercent";
            else if (sortColumnIndex == "5") sortColumn = "OnBillDiscount";
            else if (sortColumnIndex == "6") sortColumn = "SlabPercent";
            else if (sortColumnIndex == "7") sortColumn = "GrossIncentive";
            else if (sortColumnIndex == "8") sortColumn = "TdsAmount";
            else if (sortColumnIndex == "9") sortColumn = "NetTransferAmount";
            else if (sortColumnIndex == "10") sortColumn = "PaymentStatus";

            var orderedQuery = sortDirection == "desc"
                ? sortColumn switch
                {
                    "Year" => baseQuery.OrderByDescending(x => x.s.Year).ThenByDescending(x => x.s.Month),
                    "PartyCode" => baseQuery.OrderByDescending(x => x.s.PartyCode),
                    "SaleValue" => baseQuery.OrderByDescending(x => x.s.SaleValue),
                    "AchievementPercent" => baseQuery.OrderByDescending(x => x.s.AchievementPercent),
                    "OnBillDiscount" => baseQuery.OrderByDescending(x => x.s.OnBillDiscount),
                    "SlabPercent" => baseQuery.OrderByDescending(x => x.s.SlabPercent),
                    "GrossIncentive" => baseQuery.OrderByDescending(x => x.s.GrossIncentive),
                    "TdsAmount" => baseQuery.OrderByDescending(x => x.s.TdsAmount),
                    "NetTransferAmount" => baseQuery.OrderByDescending(x => x.s.NetTransferAmount),
                    "PaymentStatus" => baseQuery.OrderByDescending(x => x.s.PaymentStatus),
                    _ => baseQuery.OrderByDescending(x => x.s.Id)
                }
                : sortColumn switch
                {
                    "Year" => baseQuery.OrderBy(x => x.s.Year).ThenBy(x => x.s.Month),
                    "PartyCode" => baseQuery.OrderBy(x => x.s.PartyCode),
                    "SaleValue" => baseQuery.OrderBy(x => x.s.SaleValue),
                    "AchievementPercent" => baseQuery.OrderBy(x => x.s.AchievementPercent),
                    "OnBillDiscount" => baseQuery.OrderBy(x => x.s.OnBillDiscount),
                    "SlabPercent" => baseQuery.OrderBy(x => x.s.SlabPercent),
                    "GrossIncentive" => baseQuery.OrderBy(x => x.s.GrossIncentive),
                    "TdsAmount" => baseQuery.OrderBy(x => x.s.TdsAmount),
                    "NetTransferAmount" => baseQuery.OrderBy(x => x.s.NetTransferAmount),
                    "PaymentStatus" => baseQuery.OrderBy(x => x.s.PaymentStatus),
                    _ => baseQuery.OrderBy(x => x.s.Id)
                };

            var dataList = await orderedQuery
                .Skip(start)
                .Take(length)
                .ToListAsync(cancellationToken);

            var data = dataList.Select(x => {
                var empCode = x.b == null ? "EMP-CORP" : (string.IsNullOrEmpty(x.b.Incharge) ? "EMP-CORP" : "EMP-" + x.b.Code);
                var empName = x.b == null ? "HO Executive" : (string.IsNullOrEmpty(x.b.Incharge) ? "HO Executive" : x.b.Incharge);
                var netIncentive = Math.Max(0m, x.s.GrossIncentive - x.s.OnBillDiscount);
                var tdsPercent = netIncentive > 0 ? Math.Round((x.s.TdsAmount / netIncentive) * 100m, 2) : 0m;

                return new {
                    x.s.Id,
                    LedgerRef = $"IL-{x.s.Year}-{x.s.Month:D2}-{x.s.PartyCode}",
                    PeriodLabel = x.s.Month == 0 ? $"FY {x.s.Year}" : $"{x.s.Month:D2}/{x.s.Year}",
                    IncentiveMonth = x.s.Month,
                    IncentiveYear = x.s.Year,
                    EmployeeCode = empCode,
                    EmployeeName = empName,
                    x.s.PartyCode,
                    x.s.PartyName,
                    x.s.SaleValue,
                    x.s.OnBillDiscount,
                    x.s.AchievementPercent,
                    SlabApplied = $"{(x.s.SlabPercent * 100):0.##}%",
                    SlabVersion = 1,
                    x.s.GrossIncentive,
                    TdsPercent = tdsPercent,
                    x.s.TdsAmount,
                    x.s.NetTransferAmount,
                    x.s.PaymentStatus,
                    utrNumber = x.s.UTRNumber,
                    createdDate = x.s.CreatedAt.ToString("dd-MMM-yyyy"),
                    paymentDate = x.s.PaymentDate.HasValue ? x.s.PaymentDate.Value.ToString("dd-MMM-yyyy") : "-",
                    x.s.Remarks,
                    VersionNumber = 1,
                    locationCode = !string.IsNullOrEmpty(x.s.SourceLocation) ? x.s.SourceLocation : (x.b != null ? x.b.Code : "-"),
                    bankAccountNumber = !string.IsNullOrEmpty(x.s.BankAccountNumber) ? x.s.BankAccountNumber : "-",
                    ifsc = !string.IsNullOrEmpty(x.s.IFSC) ? x.s.IFSC : "-",
                    beneficiaryName = !string.IsNullOrEmpty(x.s.BeneficiaryName) ? x.s.BeneficiaryName : "-",
                    panNo = x.bd != null ? x.bd.PAN : "-"
                };
            }).ToList();

            return Json(new {
                draw,
                recordsTotal,
                recordsFiltered,
                data
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // =========================================================
    // LEDGER DETAIL
    // =========================================================
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        var ledger = await db.SsIncentives
            .AsNoTracking()
            .Include(x => x.ImportLog)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (ledger == null) return NotFound();

        // Security check for Sales Executive
        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var isMapped = await db.PartyExecutiveMappings
                .AsNoTracking()
                .AnyAsync(x => x.ExecutiveCode == currentUser.UserName && x.PartyCode == ledger.PartyCode, cancellationToken);
            if (!isMapped) return Forbid();
        }


        // 3-Date description string requested by user
        var monthNames = new[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" };
        var monthStr = ledger.Month > 0 && ledger.Month <= 12 ? monthNames[ledger.Month - 1] : "Historical";
        var createdStr = ledger.CreatedAt.ToString("MMMM yyyy");
        var paidStr = ledger.PaymentDate.HasValue ? ledger.PaymentDate.Value.ToString("MMMM yyyy") : "a future period";

        ViewBag.TemporalDescription = $"This incentive belongs to {monthStr} {ledger.Year}, was generated in {createdStr}, and paid in {paidStr}.";

        var party = await db.Parties.AsNoTracking().Include(p => p.Branch).FirstOrDefaultAsync(p => p.PartyCode == ledger.PartyCode, cancellationToken);
        var branch = party?.Branch;
        ViewBag.EmployeeCode = branch == null ? "EMP-CORP" : (string.IsNullOrEmpty(branch.Incharge) ? "EMP-CORP" : "EMP-" + branch.Code);
        ViewBag.EmployeeName = branch == null ? "HO Executive" : (string.IsNullOrEmpty(branch.Incharge) ? "HO Executive" : branch.Incharge);

        return View(ledger);
    }

    // =========================================================
    // CALCULATIONS VERSION HISTORY
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetHistory(string partyCode, int year, int month, CancellationToken cancellationToken)
    {
        // Security check for Sales Executive
        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var isMapped = await db.PartyExecutiveMappings
                .AsNoTracking()
                .AnyAsync(x => x.ExecutiveCode == currentUser.UserName && x.PartyCode == partyCode, cancellationToken);
            if (!isMapped) return Forbid();
        }


        var history = await db.SsIncentives
            .AsNoTracking()
            .Include(x => x.ImportLog)
            .Where(x => x.PartyCode == partyCode && x.Year == year && x.Month == month && !x.IsDeleted)
            .Select(x => new {
                x.Id,
                VersionNumber = 1,
                x.GrossIncentive,
                x.TdsAmount,
                x.NetTransferAmount,
                x.PaymentStatus,
                UploadedBy = x.ImportLog != null ? x.ImportLog.CreatedBy : "system",
                UploadedAt = x.ImportLog != null ? x.ImportLog.CreatedAt.ToString("g") : x.CreatedAt.ToString("g"),
                ChangeReason = x.Remarks ?? "No reason documented",
                IsLatestVersion = true
            })
            .ToListAsync(cancellationToken);

        return Json(history);
    }

    // =========================================================
    // CALCULATIONS VERSION DIFF
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> GetDiff(long currentId, long compareId, CancellationToken cancellationToken)
    {
        var current = await db.SsIncentives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == currentId, cancellationToken);
        var compare = await db.SsIncentives.AsNoTracking().FirstOrDefaultAsync(x => x.Id == compareId, cancellationToken);

        if (current == null || compare == null)
        {
            return BadRequest(new { message = "One or both ledger versions not found." });
        }

        // Security check for Sales Executive
        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var isMapped = await db.PartyExecutiveMappings
                .AsNoTracking()
                .AnyAsync(x => x.ExecutiveCode == currentUser.UserName && (x.PartyCode == current.PartyCode || x.PartyCode == compare.PartyCode), cancellationToken);
            if (!isMapped) return Forbid();
        }


        return Json(new {
            current = new {
                VersionNumber = 1,
                current.SaleValue,
                OnBillDiscount = current.OnBillDiscount,
                current.AchievementPercent,
                SlabApplied = $"{(current.SlabPercent * 100):0.##}%",
                current.GrossIncentive,
                TdsPercent = current.GrossIncentive > 0 ? Math.Round((current.TdsAmount / current.GrossIncentive) * 100m, 2) : 0m,
                current.TdsAmount,
                current.NetTransferAmount
            },
            compare = new {
                VersionNumber = 1,
                compare.SaleValue,
                OnBillDiscount = compare.OnBillDiscount,
                compare.AchievementPercent,
                SlabApplied = $"{(compare.SlabPercent * 100):0.##}%",
                compare.GrossIncentive,
                TdsPercent = compare.GrossIncentive > 0 ? Math.Round((compare.TdsAmount / compare.GrossIncentive) * 100m, 2) : 0m,
                compare.TdsAmount,
                compare.NetTransferAmount
            }
        });
    }

    // =========================================================
    // HIGH-PERFORMANCE LEDGER EXPORTS
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> ExportExcel(string? filterPeriod, string? filterStatus, string? search, CancellationToken cancellationToken)
    {
        var baseQuery = from s in db.SsIncentives.AsNoTracking()
                        join p in db.Parties.AsNoTracking() on s.PartyCode equals p.PartyCode into pGroup
                        from p in pGroup.DefaultIfEmpty()
                        join b in db.Branches.AsNoTracking() on (p != null ? p.BranchId : 0) equals b.Id into bGroup
                        from b in bGroup.DefaultIfEmpty()
                        where !s.IsDeleted
                        select new { s, p, b };

        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var mappedPartyCodes = await db.PartyExecutiveMappings
                .AsNoTracking()
                .Where(x => x.ExecutiveCode == currentUser.UserName)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            baseQuery = baseQuery.Where(x => mappedPartyCodes.Contains(x.s.PartyCode));
        }


        if (!string.IsNullOrEmpty(filterPeriod) && filterPeriod != "all")
        {
            var parts = filterPeriod.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var y))
            {
                baseQuery = baseQuery.Where(x => x.s.Month == m && x.s.Year == y);
            }
        }

        if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "all")
        {
            baseQuery = baseQuery.Where(x => x.s.PaymentStatus == filterStatus);
        }

        if (!string.IsNullOrEmpty(search))
        {
            baseQuery = baseQuery.Where(x => x.s.PartyCode.Contains(search) || 
                                             x.s.PartyName.Contains(search) || 
                                             (x.b != null && x.b.Incharge.Contains(search)) ||
                                             (x.b != null && x.b.Code.Contains(search)));
        }

        var projectedQuery = baseQuery.Select(x => new 
        {
            x.s.Year,
            x.s.Month,
            x.s.PartyCode,
            x.s.PartyName,
            x.s.SaleValue,
            x.s.OnBillDiscount,
            x.s.AchievementPercent,
            x.s.SlabPercent,
            x.s.GrossIncentive,
            x.s.TdsAmount,
            x.s.NetTransferAmount,
            x.s.PaymentStatus,
            x.s.UTRNumber,
            x.s.CreatedAt,
            x.s.PaymentDate,
            BranchCode = x.b != null ? x.b.Code : null,
            BranchIncharge = x.b != null ? x.b.Incharge : null
        });

        var dataList = await projectedQuery
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        using var workbook = new ClosedXML.Excel.XLWorkbook();
        var sheet = workbook.Worksheets.Add("Incentive Ledger");

        var headers = new[]
        {
            "Ledger Ref", "Incentive Year", "Incentive Month", "Employee Code", "Employee Name",
            "Dealer Code", "Dealer Name", "Sales Value", "On-Bill Discount", "Slab Rate %",
            "Slab Applied", "Slab Version", "Gross Incentive", "TDS %", "TDS Amount",
            "Net Transfer Amount", "Payment Status", "UTR Number", "Created Date", "Payment Date"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#093375");
            cell.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
        }

        var row = 2;
        foreach (var item in dataList)
        {
            var empCode = string.IsNullOrEmpty(item.BranchCode) ? "EMP-CORP" : "EMP-" + item.BranchCode;
            var empName = string.IsNullOrEmpty(item.BranchIncharge) ? "HO Executive" : item.BranchIncharge;
            var tdsPercent = item.GrossIncentive > 0 ? Math.Round((item.TdsAmount / item.GrossIncentive) * 100m, 2) : 0m;

            sheet.Cell(row, 1).Value = $"IL-{item.Year}-{item.Month:D2}-{item.PartyCode}";
            sheet.Cell(row, 2).Value = item.Year;
            sheet.Cell(row, 3).Value = item.Month;
            sheet.Cell(row, 4).Value = empCode;
            sheet.Cell(row, 5).Value = empName;
            sheet.Cell(row, 6).Value = item.PartyCode;
            sheet.Cell(row, 7).Value = item.PartyName;
            sheet.Cell(row, 8).Value = (double)item.SaleValue;
            sheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 9).Value = (double)item.OnBillDiscount;
            sheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 10).Value = (double)item.AchievementPercent;
            sheet.Cell(row, 10).Style.NumberFormat.Format = "0.00\"\\%\"";
            sheet.Cell(row, 11).Value = $"{(item.SlabPercent * 100):0.##}%";
            sheet.Cell(row, 12).Value = 1;
            sheet.Cell(row, 13).Value = (double)item.GrossIncentive;
            sheet.Cell(row, 13).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 14).Value = (double)tdsPercent / 100.0;
            sheet.Cell(row, 14).Style.NumberFormat.Format = "0.0%";
            sheet.Cell(row, 15).Value = (double)item.TdsAmount;
            sheet.Cell(row, 15).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 16).Value = (double)item.NetTransferAmount;
            sheet.Cell(row, 16).Style.NumberFormat.Format = "#,##0";
            sheet.Cell(row, 17).Value = item.PaymentStatus;
            sheet.Cell(row, 18).Value = item.UTRNumber;
            sheet.Cell(row, 19).Value = item.CreatedAt.ToString("dd-MMM-yyyy");
            sheet.Cell(row, 20).Value = item.PaymentDate.HasValue ? item.PaymentDate.Value.ToString("dd-MMM-yyyy") : "-";
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new System.IO.MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Incentive_Ledger_{DateTime.Now:yyyyMMddHHmm}.xlsx");
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? filterPeriod, string? filterStatus, string? search, CancellationToken cancellationToken)
    {
        var baseQuery = from s in db.SsIncentives.AsNoTracking()
                        join p in db.Parties.AsNoTracking() on s.PartyCode equals p.PartyCode into pGroup
                        from p in pGroup.DefaultIfEmpty()
                        join b in db.Branches.AsNoTracking() on (p != null ? p.BranchId : 0) equals b.Id into bGroup
                        from b in bGroup.DefaultIfEmpty()
                        where !s.IsDeleted
                        select new { s, p, b };

        if (currentUser.IsInRole(AppRoles.SalesExecutive))
        {
            var mappedPartyCodes = await db.PartyExecutiveMappings
                .AsNoTracking()
                .Where(x => x.ExecutiveCode == currentUser.UserName)
                .Select(x => x.PartyCode)
                .ToListAsync(cancellationToken);
            baseQuery = baseQuery.Where(x => mappedPartyCodes.Contains(x.s.PartyCode));
        }


        if (!string.IsNullOrEmpty(filterPeriod) && filterPeriod != "all")
        {
            var parts = filterPeriod.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var m) && int.TryParse(parts[1], out var y))
            {
                baseQuery = baseQuery.Where(x => x.s.Month == m && x.s.Year == y);
            }
        }

        if (!string.IsNullOrEmpty(filterStatus) && filterStatus != "all")
        {
            baseQuery = baseQuery.Where(x => x.s.PaymentStatus == filterStatus);
        }

        if (!string.IsNullOrEmpty(search))
        {
            baseQuery = baseQuery.Where(x => x.s.PartyCode.Contains(search) || 
                                             x.s.PartyName.Contains(search) || 
                                             (x.b != null && x.b.Incharge.Contains(search)) ||
                                             (x.b != null && x.b.Code.Contains(search)));
        }

        var projectedQuery = baseQuery.Select(x => new 
        {
            x.s.Year,
            x.s.Month,
            x.s.PartyCode,
            x.s.PartyName,
            x.s.SaleValue,
            x.s.OnBillDiscount,
            x.s.AchievementPercent,
            x.s.SlabPercent,
            x.s.GrossIncentive,
            x.s.TdsAmount,
            x.s.NetTransferAmount,
            x.s.PaymentStatus,
            x.s.UTRNumber,
            x.s.CreatedAt,
            x.s.PaymentDate,
            BranchCode = x.b != null ? x.b.Code : null,
            BranchIncharge = x.b != null ? x.b.Incharge : null
        });

        var dataList = await projectedQuery
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        var builder = new System.Text.StringBuilder();
        builder.AppendLine("LedgerRef,IncentiveYear,IncentiveMonth,EmployeeCode,EmployeeName,PartyCode,PartyName,SaleValue,OnBillDiscount,SlabRatePercent,SlabApplied,SlabVersion,GrossIncentive,TdsPercent,TdsAmount,NetTransferAmount,PaymentStatus,UTRNumber,CreatedDate,PaymentDate");

        foreach (var item in dataList)
        {
            var empCode = string.IsNullOrEmpty(item.BranchCode) ? "EMP-CORP" : "EMP-" + item.BranchCode;
            var empName = string.IsNullOrEmpty(item.BranchIncharge) ? "HO Executive" : item.BranchIncharge;
            var tdsPercent = item.GrossIncentive > 0 ? Math.Round((item.TdsAmount / item.GrossIncentive) * 100m, 2) : 0m;
            var ledgerRef = $"IL-{item.Year}-{item.Month:D2}-{item.PartyCode}";

            builder.AppendLine($"\"{ledgerRef}\",{item.Year},{item.Month},\"{empCode}\",\"{empName}\",\"{item.PartyCode}\",\"{item.PartyName}\",{item.SaleValue},{item.OnBillDiscount},{item.AchievementPercent},\"{(item.SlabPercent * 100):0.##}%\",1,{item.GrossIncentive},{tdsPercent},{item.TdsAmount},{item.NetTransferAmount},\"{item.PaymentStatus}\",\"{item.UTRNumber}\",\"{item.CreatedAt:yyyy-MM-dd}\",\"{(item.PaymentDate.HasValue ? item.PaymentDate.Value.ToString("yyyy-MM-dd") : "-")}\"");
        }

        return File(System.Text.Encoding.UTF8.GetBytes(builder.ToString()), "text/csv", $"Incentive_Ledger_{DateTime.Now:yyyyMMddHHmm}.csv");
    }

    [HttpPost]
    [Authorize(Roles = "Super Admin,HO Finance")]
    public async Task<IActionResult> OverrideCalculation(long id, decimal grossOverride, string remarks, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(remarks))
        {
            return BadRequest(new { message = "Remarks are mandatory for manual override." });
        }

        var ssIncentive = await db.SsIncentives
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (ssIncentive == null)
        {
            return NotFound(new { message = "Incentive record not found." });
        }

        if (ssIncentive.PaymentStatus == "Paid")
        {
            return BadRequest(new { message = "Cannot override a record that has already been settled (Paid)." });
        }

        // Perform calculations
        decimal gross = Math.Round(grossOverride, 0, MidpointRounding.AwayFromZero);
        
        // Fetch bank details for party to verify PAN
        var party = await db.Parties.AsNoTracking().FirstOrDefaultAsync(p => p.PartyCode == ssIncentive.PartyCode, cancellationToken);
        var bank = party != null ? await db.BankDetails.AsNoTracking().Where(b => b.PartyId == party.Id && b.ApprovalStatus == "Approved" && !b.IsDeleted).OrderByDescending(b => b.IsPrimary).ThenByDescending(b => b.Id).FirstOrDefaultAsync(cancellationToken) : null;
        var hasPan = bank != null && !string.IsNullOrWhiteSpace(bank.PAN);

        // Resolve active TDS rule
        var activeTdsRules = await db.TdsRules
            .Where(x => !x.IsDeleted && x.EffectiveFrom <= new DateTime(ssIncentive.Year, ssIncentive.Month, 1) && x.EffectiveTo >= new DateTime(ssIncentive.Year, ssIncentive.Month, 1))
            .OrderByDescending(x => x.AnnualThreshold)
            .ToListAsync(cancellationToken);

        // Fetch prior annual incentives
        var priorAnnualIncentive = await db.SsIncentives
            .Where(x => x.PartyCode == ssIncentive.PartyCode && x.Year == ssIncentive.Year && x.Month != ssIncentive.Month && !x.IsDeleted)
            .SumAsync(x => x.GrossIncentive, cancellationToken);

        decimal totalAnnualIncentive = priorAnnualIncentive + gross;
        TdsRule? matchedTdsRule = null;
        foreach (var rule in activeTdsRules)
        {
            if (totalAnnualIncentive >= rule.AnnualThreshold)
            {
                matchedTdsRule = rule;
                break;
            }
        }

        decimal tdsPercent = matchedTdsRule != null
            ? (hasPan ? (matchedTdsRule.RateWithPan * 100m) : (matchedTdsRule.RateNoPan * 100m))
            : (hasPan ? 5m : 20m);

        decimal tds = Math.Round(gross * (tdsPercent / 100m), 0, MidpointRounding.AwayFromZero);
        decimal netEligible = Math.Max(0, gross - tds);

        // Retrieve outstanding balance dynamically from DealerOutstandings table
        var outstandingRecord = await db.DealerOutstandings
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Year == ssIncentive.Year && o.Month == ssIncentive.Month && o.PartyCode == ssIncentive.PartyCode && !o.IsDeleted, cancellationToken);
        decimal outstanding = outstandingRecord?.Outstanding ?? 0m;
        decimal adjusted = Math.Max(0m, Math.Min(netEligible, outstanding));
        decimal transfer = Math.Max(0, netEligible - adjusted);

        // Update SsIncentive Ð½ÐµÐ¿Ð¾ÑÑ€ÐµÐ´ÑÑ‚Ð²ÐµÐ½Ð½Ð¾
        ssIncentive.GrossIncentive = gross;
        ssIncentive.TdsAmount = tds;
        ssIncentive.NetTransferAmount = transfer;
        ssIncentive.IsEdited = true;
        ssIncentive.Remarks = remarks;
        ssIncentive.Status = "Pending Approval";
        ssIncentive.ApprovedBy = null;
        ssIncentive.ApprovedAt = null;

        db.Entry(ssIncentive).State = EntityState.Modified;
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = "Incentive calculation overridden successfully.", newLedgerId = ssIncentive.Id });
    }
}

