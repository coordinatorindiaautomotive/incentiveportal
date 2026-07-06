using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Services;

public record ReportFilter(string FieldName, string Operator, string Value);

public record ReportQuery(
    List<string> SelectedColumns,
    List<ReportFilter> Filters,
    string? GroupByField = null
);

public interface IReportEngineService
{
    Task<List<Dictionary<string, object>>> GenerateReportDataAsync(ReportQuery query);
    Task<byte[]> ExportToExcelAsync(string reportName, ReportQuery query);
}

public sealed class ReportEngineService(IncentiveDbContext db) : IReportEngineService
{
    public async Task<List<Dictionary<string, object>>> GenerateReportDataAsync(ReportQuery query)
    {
        var rawData = await db.SsIncentives.AsNoTracking().ToListAsync();
        var resultList = new List<Dictionary<string, object>>();

        // Apply filters in-memory (safest and simplest for dynamic filter configuration)
        var filteredData = rawData.Where(ledger => EvaluateFilters(ledger, query.Filters)).ToList();

        // If Group By is specified, perform aggregation
        if (!string.IsNullOrWhiteSpace(query.GroupByField))
        {
            var propInfo = typeof(SsIncentive).GetProperty(query.GroupByField, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propInfo != null)
            {
                var grouped = filteredData.GroupBy(x => propInfo.GetValue(x)?.ToString() ?? "Unknown");
                foreach (var group in grouped)
                {
                    var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    dict[query.GroupByField] = group.Key;
                    
                    // Add summary metrics
                    if (query.SelectedColumns.Contains("SaleValue", StringComparer.OrdinalIgnoreCase))
                        dict["SaleValue"] = group.Sum(x => x.SaleValue);
                    if (query.SelectedColumns.Contains("GrossIncentive", StringComparer.OrdinalIgnoreCase))
                        dict["GrossIncentive"] = group.Sum(x => x.GrossIncentive);
                    if (query.SelectedColumns.Contains("TdsAmount", StringComparer.OrdinalIgnoreCase))
                        dict["TdsAmount"] = group.Sum(x => x.TdsAmount);
                    if (query.SelectedColumns.Contains("NetTransferAmount", StringComparer.OrdinalIgnoreCase))
                        dict["NetTransferAmount"] = group.Sum(x => x.NetTransferAmount);
                    
                    // Fill other fields with generic grouping headers
                    foreach (var col in query.SelectedColumns)
                    {
                        if (!dict.ContainsKey(col))
                        {
                            dict[col] = "---";
                        }
                    }
                    resultList.Add(dict);
                }
                return resultList;
            }
        }

        // Standard projection of selected columns
        foreach (var ledger in filteredData)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in query.SelectedColumns)
            {
                var prop = typeof(SsIncentive).GetProperty(col, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (prop != null)
                {
                    dict[col] = prop.GetValue(ledger) ?? DBNull.Value;
                }
                else
                {
                    dict[col] = DBNull.Value;
                }
            }
            resultList.Add(dict);
        }

        return resultList;
    }

    public async Task<byte[]> ExportToExcelAsync(string reportName, ReportQuery query)
    {
        var data = await GenerateReportDataAsync(query);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(reportName);

        // 1. Write headers
        for (int i = 0; i < query.SelectedColumns.Count; i++)
        {
            sheet.Cell(1, i + 1).Value = query.SelectedColumns[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
            sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        // 2. Write rows
        int rIndex = 2;
        foreach (var row in data)
        {
            for (int cIndex = 0; cIndex < query.SelectedColumns.Count; cIndex++)
            {
                var colName = query.SelectedColumns[cIndex];
                if (row.TryGetValue(colName, out var cellVal))
                {
                    if (cellVal is decimal decVal)
                    {
                        sheet.Cell(rIndex, cIndex + 1).SetValue(decVal);
                    }
                    else if (cellVal is double dbVal)
                    {
                        sheet.Cell(rIndex, cIndex + 1).SetValue(dbVal);
                    }
                    else if (cellVal is int intVal)
                    {
                        sheet.Cell(rIndex, cIndex + 1).SetValue(intVal);
                    }
                    else if (cellVal is DateTime dtVal)
                    {
                        sheet.Cell(rIndex, cIndex + 1).SetValue(dtVal);
                    }
                    else
                    {
                        sheet.Cell(rIndex, cIndex + 1).SetValue(cellVal?.ToString() ?? string.Empty);
                    }
                }
            }
            rIndex++;
        }

        sheet.Columns().AdjustToContents();
        sheet.RangeUsed().Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.RangeUsed().Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static bool EvaluateFilters(SsIncentive ledger, List<ReportFilter> filters)
    {
        if (filters == null || !filters.Any())
            return true;

        foreach (var filter in filters)
        {
            var prop = typeof(SsIncentive).GetProperty(filter.FieldName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) continue;

            var val = prop.GetValue(ledger);
            if (val == null) return false;

            var actualStr = val.ToString() ?? string.Empty;
            var op = filter.Operator.ToUpperInvariant().Trim();
            var targetVal = filter.Value.Trim();

            if (decimal.TryParse(actualStr, out var actualDec) && decimal.TryParse(targetVal, out var targetDec))
            {
                bool match = op switch
                {
                    ">=" => actualDec >= targetDec,
                    "<=" => actualDec <= targetDec,
                    ">" => actualDec > targetDec,
                    "<" => actualDec < targetDec,
                    "==" or "=" => actualDec == targetDec,
                    "!=" or "<>" => actualDec != targetDec,
                    _ => false
                };
                if (!match) return false;
            }
            else
            {
                bool match = op switch
                {
                    "==" or "=" => actualStr.Equals(targetVal, StringComparison.OrdinalIgnoreCase),
                    "!=" or "<>" => !actualStr.Equals(targetVal, StringComparison.OrdinalIgnoreCase),
                    "CONTAINS" => actualStr.Contains(targetVal, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
                if (!match) return false;
            }
        }

        return true;
    }
}
