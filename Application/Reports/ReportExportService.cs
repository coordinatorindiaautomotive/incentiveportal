using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IReportExportService
{
    Task<byte[]> ExportIncentiveRegisterAsync(IncentiveRegisterViewModel model, CancellationToken cancellationToken);
    Task<byte[]> ExportRawSalesAsync(int month, int year, CancellationToken cancellationToken);
    Task<byte[]> ExportOutstandingMasterAsync(List<OutstandingMasterRow> model, CancellationToken cancellationToken);
    Task<byte[]> ExportTargetVsAchievementAsync(TargetVsAchievementViewModel model, CancellationToken cancellationToken);
}

public sealed class ReportExportService(IncentiveDbContext db) : IReportExportService
{
    public async Task<byte[]> ExportIncentiveRegisterAsync(IncentiveRegisterViewModel model, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Incentive Register");

        var branchMap = await db.Parties
            .AsNoTracking()
            .Include(p => p.Branch)
            .Where(p => !p.IsDeleted)
            .ToDictionaryAsync(p => p.PartyCode, p => p.Branch?.Code ?? "-", StringComparer.OrdinalIgnoreCase, cancellationToken);

        var visibleColumns = new List<ReportColumnConfig>
        {
            new() { ColumnKey = "MonthLabel", DisplayName = "Month", SortOrder = 1 },
            new() { ColumnKey = "PartyCode", DisplayName = "Party Code", SortOrder = 2 },
            new() { ColumnKey = "PartyName", DisplayName = "Party Name", SortOrder = 3 },
            new() { ColumnKey = "BranchCode", DisplayName = "Branch", SortOrder = 4 },
            new() { ColumnKey = "SalesExecutive", DisplayName = "Sales Executive", SortOrder = 5 },
            new() { ColumnKey = "SaleValue", DisplayName = "Sale Value", SortOrder = 6, Format = "C" },
            new() { ColumnKey = "OnBillDiscount", DisplayName = "On Bill Discount", SortOrder = 7, Format = "C" },
            new() { ColumnKey = "AchievementPercent", DisplayName = "Achievement %", SortOrder = 8, Format = "P" },
            new() { ColumnKey = "TdsAmount", DisplayName = "TDS Amount", SortOrder = 9, Format = "C" },
            new() { ColumnKey = "NetTransferAmount", DisplayName = "Net Payout", SortOrder = 10, Format = "C" },
            new() { ColumnKey = "PaymentStatus", DisplayName = "Payment Status", SortOrder = 11 },
            new() { ColumnKey = "UTRNumber", DisplayName = "UTR Number", SortOrder = 12 },
            new() { ColumnKey = "PaymentDate", DisplayName = "Payment Date", SortOrder = 13 },
            new() { ColumnKey = "BeneficiaryName", DisplayName = "Beneficiary Name", SortOrder = 14 },
            new() { ColumnKey = "BankAccountNumber", DisplayName = "Beneficiary Account No", SortOrder = 15 },
            new() { ColumnKey = "IFSC", DisplayName = "Bene_IFSC_Code", SortOrder = 16 },
            new() { ColumnKey = "PanNo", DisplayName = "PAN No", SortOrder = 17 }
        };

        for (var i = 0; i < visibleColumns.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = visibleColumns[i].DisplayName;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#0d52d6");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var item in model.Rows)
        {
            for (var colIdx = 0; colIdx < visibleColumns.Count; colIdx++)
            {
                var col = visibleColumns[colIdx];
                var cell = sheet.Cell(row, colIdx + 1);

                switch (col.ColumnKey)
                {
                    case "MonthLabel":
                        cell.Value = item.MonthLabel;
                        break;
                    case "PartyCode":
                        cell.Value = item.PartyCode;
                        break;
                    case "PartyName":
                        cell.Value = item.PartyName;
                        break;
                    case "BranchCode":
                        cell.Value = item.BranchCode;
                        break;
                    case "SalesExecutive":
                        cell.Value = item.SalesExecutive;
                        break;
                    case "SaleValue":
                        cell.Value = item.SaleValue;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "SlabPercent":
                        cell.Value = item.SlabPercent / 100.0m;
                        cell.Style.NumberFormat.Format = "0.0%";
                        break;
                    case "OnBillDiscount":
                        cell.Value = item.OnBillDiscount;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "AchievementPercent":
                        cell.Value = item.AchievementPercent / 100.0m;
                        cell.Style.NumberFormat.Format = "0.0%";
                        break;
                    case "GrossIncentive":
                        cell.Value = item.GrossIncentive;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "TdsAmount":
                        cell.Value = item.TdsAmount;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "AdjustedAmount":
                        var adjusted = item.GrossIncentive - item.TdsAmount - (item.NetTransferAmount ?? 0);
                        cell.Value = adjusted;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "TransferAmount":
                    case "NetTransferAmount":
                        cell.Value = item.NetTransferAmount ?? 0;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "TransferredAmount":
                        cell.Value = item.TransferredAmount;
                        cell.Style.NumberFormat.Format = "#,##0";
                        break;
                    case "ProcessingDate":
                        cell.Value = item.ProcessingDate.HasValue ? item.ProcessingDate.Value.ToString("dd-MMM-yyyy") : "-";
                        break;
                    case "PaymentDate":
                        cell.Value = item.PaymentDate.HasValue ? item.PaymentDate.Value.ToString("dd-MMM-yyyy") : "-";
                        break;
                    case "PaymentStatus":
                        cell.Value = item.PaymentStatus;
                        break;
                    case "UTRNumber":
                        cell.Value = item.UTRNumber ?? "-";
                        break;
                    case "BankAccountNumber":
                        cell.Value = item.BankAccountNumber;
                        break;
                    case "IFSC":
                        cell.Value = item.IFSC;
                        break;
                    case "BeneficiaryName":
                        cell.Value = item.BeneficiaryName;
                        break;
                    case "PanNo":
                        cell.Value = item.PanNo;
                        break;
                    case "BankDetails":
                        cell.Value = $"{item.BeneficiaryName} | A/c: {item.BankAccountNumber} | IFSC: {item.IFSC}";
                        break;
                    default:
                        cell.Value = "-";
                        break;
                }
            }
            row++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportRawSalesAsync(int month, int year, CancellationToken cancellationToken)
    {
        var sales = await db.Raws
            .Where(x => x.MonthNumber == month && x.YearNumber == year && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Raw Sales Data");
        
        var headers = new[] 
        { 
            "Dealer Sub-Type", "Consignee", "Dealer Code", "Loc", "Part Category Code", "Fiscal Year", "Quarter", "Month", "Month Year", "Cons Party Code", "Cons Party Name", "Party Type", "Document Num", "Remarks", "Net Retail Selling", "Discount Amount", "Net Retail DDL", "Original Code"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1b5e20"); // Harmonies with raw sheets in dark green
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var item in sales)
        {
            sheet.Cell(row, 1).Value = item.DealerSubType ?? string.Empty;
            sheet.Cell(row, 2).Value = item.Consignee ?? string.Empty;
            sheet.Cell(row, 3).Value = item.DealerCode ?? string.Empty;
            sheet.Cell(row, 4).Value = item.Loc ?? string.Empty;
            sheet.Cell(row, 5).Value = item.PartCategoryCode ?? string.Empty;
            sheet.Cell(row, 6).Value = item.FiscalYear ?? string.Empty;
            sheet.Cell(row, 7).Value = item.Quarter ?? string.Empty;
            sheet.Cell(row, 8).Value = item.Month ?? string.Empty;
            sheet.Cell(row, 9).Value = item.MonthYear ?? string.Empty;
            sheet.Cell(row, 10).Value = item.ConsPartyCode ?? string.Empty;
            sheet.Cell(row, 11).Value = item.ConsPartyName ?? string.Empty;
            sheet.Cell(row, 12).Value = item.PartyType ?? string.Empty;
            sheet.Cell(row, 13).Value = item.DocumentNum ?? string.Empty;
            sheet.Cell(row, 14).Value = item.Remarks ?? string.Empty;
            sheet.Cell(row, 15).Value = item.NetRetailSelling;
            sheet.Cell(row, 15).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 16).Value = item.DiscountAmount;
            sheet.Cell(row, 16).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 17).Value = item.NetRetailDdl;
            sheet.Cell(row, 17).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 18).Value = item.OriginalCode ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();
        sheet.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportOutstandingMasterAsync(List<OutstandingMasterRow> model, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Outstanding Master");

        // Enable summary row above outlines
        sheet.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;

        // Header Row 1: Merged Values Header
        sheet.Cell(1, 1).Value = "";
        sheet.Cell(1, 2).Value = "";
        var valuesHeader = sheet.Cell(1, 3);
        valuesHeader.Value = "Values";
        sheet.Range("C1:K1").Merge();
        valuesHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        valuesHeader.Style.Font.Bold = true;
        valuesHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#C5D9F1");

        // Header Row 2
        var headers2 = new[]
        {
            "BRANCH", "Particulars", "Pending Bills", "< 7 days", "7 to 14 days", 
            "14 to 21 days", "21 to 28 days", "28 to 35 days", "35 to 50 days", 
            "50 to 80 days", "> 80 days"
        };

        for (var i = 0; i < headers2.Length; i++)
        {
            var cell = sheet.Cell(2, i + 1);
            cell.Value = headers2[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C5D9F1");
            cell.Style.Alignment.Horizontal = i >= 2 ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
        }

        var headerRange = sheet.Range("A1:K2");
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        var grouped = model.GroupBy(x => new { x.BranchCode, x.BranchName })
            .OrderBy(g => g.Key.BranchName)
            .ToList();

        var row = 3;
        foreach (var group in grouped)
        {
            // Branch Summary Row
            sheet.Cell(row, 1).Value = $"+ {group.Key.BranchName}";
            sheet.Cell(row, 2).Value = "";
            sheet.Cell(row, 3).Value = group.Sum(x => x.Outstanding);
            sheet.Cell(row, 4).Value = group.Sum(x => x.OutstandingLess7Days ?? 0m);
            sheet.Cell(row, 5).Value = group.Sum(x => x.Outstanding7To14Days ?? 0m);
            sheet.Cell(row, 6).Value = group.Sum(x => x.Outstanding14To21Days ?? 0m);
            sheet.Cell(row, 7).Value = group.Sum(x => x.Outstanding21To28Days ?? 0m);
            sheet.Cell(row, 8).Value = group.Sum(x => x.Outstanding28To35Days ?? 0m);
            sheet.Cell(row, 9).Value = group.Sum(x => x.Outstanding35To50Days ?? 0m);
            sheet.Cell(row, 10).Value = group.Sum(x => x.Outstanding50To80Days ?? 0m);
            sheet.Cell(row, 11).Value = group.Sum(x => x.OutstandingMore80Days ?? 0m);

            for (int col = 1; col <= 11; col++)
            {
                var cell = sheet.Cell(row, col);
                cell.Style.Font.Bold = true;
                if (col >= 3)
                {
                    cell.Style.NumberFormat.Format = "#,##0";
                }
            }

            row++;

            // Dealer Rows (grouped under the Branch Row)
            foreach (var item in group)
            {
                sheet.Cell(row, 1).Value = item.PartyCode;
                sheet.Cell(row, 2).Value = item.PartyName;
                sheet.Cell(row, 3).Value = item.Outstanding;
                sheet.Cell(row, 4).Value = item.OutstandingLess7Days ?? 0m;
                sheet.Cell(row, 5).Value = item.Outstanding7To14Days ?? 0m;
                sheet.Cell(row, 6).Value = item.Outstanding14To21Days ?? 0m;
                sheet.Cell(row, 7).Value = item.Outstanding21To28Days ?? 0m;
                sheet.Cell(row, 8).Value = item.Outstanding28To35Days ?? 0m;
                sheet.Cell(row, 9).Value = item.Outstanding35To50Days ?? 0m;
                sheet.Cell(row, 10).Value = item.Outstanding50To80Days ?? 0m;
                sheet.Cell(row, 11).Value = item.OutstandingMore80Days ?? 0m;

                sheet.Row(row).Group();
                sheet.Row(row).Collapse();

                for (int col = 3; col <= 11; col++)
                {
                    sheet.Cell(row, col).Style.NumberFormat.Format = "#,##0";
                }

                row++;
            }
        }

        // Grand Total Row
        sheet.Cell(row, 1).Value = "Grand Total";
        sheet.Cell(row, 2).Value = "";
        sheet.Cell(row, 3).Value = model.Sum(x => x.Outstanding);
        sheet.Cell(row, 4).Value = model.Sum(x => x.OutstandingLess7Days ?? 0m);
        sheet.Cell(row, 5).Value = model.Sum(x => x.Outstanding7To14Days ?? 0m);
        sheet.Cell(row, 6).Value = model.Sum(x => x.Outstanding14To21Days ?? 0m);
        sheet.Cell(row, 7).Value = model.Sum(x => x.Outstanding21To28Days ?? 0m);
        sheet.Cell(row, 8).Value = model.Sum(x => x.Outstanding28To35Days ?? 0m);
        sheet.Cell(row, 9).Value = model.Sum(x => x.Outstanding35To50Days ?? 0m);
        sheet.Cell(row, 10).Value = model.Sum(x => x.Outstanding50To80Days ?? 0m);
        sheet.Cell(row, 11).Value = model.Sum(x => x.OutstandingMore80Days ?? 0m);

        for (int col = 1; col <= 11; col++)
        {
            var cell = sheet.Cell(row, col);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C5D9F1");
            if (col >= 3)
            {
                cell.Style.NumberFormat.Format = "#,##0";
            }
        }

        var fullRange = sheet.Range(1, 1, row, 11);
        fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        fullRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportTargetVsAchievementAsync(TargetVsAchievementViewModel model, CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Target vs Achievement");

        var headers = new[] { "Branch", "Party Code", "Party Name", "System Target", "Admin Target", "Final Target", "Current Sales", "Achievement %", "Slab Placement", "Next Slab Target", "Last Month Sales", "YTD Sales", "LY YTD Sales", "YoY Growth %" };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#093375");
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = 2;
        foreach (var item in model.Rows)
        {
            sheet.Cell(row, 1).Value = item.BranchName;
            sheet.Cell(row, 2).Value = item.PartyCode;
            sheet.Cell(row, 3).Value = item.PartyName;
            
            sheet.Cell(row, 4).Value = item.SystemSuggestedTarget;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0";
            
            if (item.AdminDefinedTarget.HasValue)
            {
                sheet.Cell(row, 5).Value = item.AdminDefinedTarget.Value;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0";
            }
            else
            {
                sheet.Cell(row, 5).Value = "-";
            }
            
            sheet.Cell(row, 6).Value = item.FinalTarget;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 7).Value = item.CurrentAchievementSales;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 8).Value = item.AchievementPercent / 100m;
            sheet.Cell(row, 8).Style.NumberFormat.Format = "0.0%";
            
            sheet.Cell(row, 9).Value = item.CurrentSlabLabel;
            
            sheet.Cell(row, 10).Value = item.NextSlabTarget;
            sheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 11).Value = item.LastMonthSales;
            sheet.Cell(row, 11).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 12).Value = item.YTDSales;
            sheet.Cell(row, 12).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 13).Value = item.LastYearYTDSales;
            sheet.Cell(row, 13).Style.NumberFormat.Format = "#,##0";
            
            sheet.Cell(row, 14).Value = item.YoYGrowthPercent / 100m;
            sheet.Cell(row, 14).Style.NumberFormat.Format = "0.0%";

            row++;
        }

        var fullRange = sheet.Range(1, 1, row - 1, 14);
        fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        fullRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

