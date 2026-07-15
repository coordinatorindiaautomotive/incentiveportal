using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

using IncentivePortal.Infrastructure.Cache;

namespace IncentivePortal.Services;

public record UploadRowError(int RowNumber, string ColumnName, string ErrorMessage);

public sealed class UploadPreviewResult
{
    public bool IsValid => Errors.Count == 0;
    public List<Dictionary<string, object>> Rows { get; set; } = new();
    public List<UploadRowError> Errors { get; set; } = new();
}

public sealed class UploadCommitResult
{
    public bool Success { get; set; }
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<UploadRowError> Errors { get; set; } = new();
}

public interface IUploadEngineService
{
    Task<UploadPreviewResult> PreviewAsync(Stream fileStream, string templateCode, CancellationToken cancellationToken);
    Task<UploadCommitResult> CommitAsync(Stream fileStream, string templateCode, string username, CancellationToken cancellationToken);
}

public sealed class UploadEngineService(IncentiveDbContext db, ILookupCacheService cache) : IUploadEngineService
{
    public async Task<UploadPreviewResult> PreviewAsync(Stream fileStream, string templateCode, CancellationToken cancellationToken)
    {
        var result = new UploadPreviewResult();

        // 1. Get the import template
        var template = await db.ImportTemplates
            .Include(t => t.Columns)
            .Include(t => t.Mappings)
            .Include(t => t.ValidationRules)
            .FirstOrDefaultAsync(t => t.Code == templateCode, cancellationToken);

        if (template == null)
        {
            result.Errors.Add(new UploadRowError(0, "Template", $"Import template with code '{templateCode}' not found."));
            return result;
        }

        // 2. Open spreadsheet
        using var workbook = new XLWorkbook(fileStream);
        var ws = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException("Workbook does not contain any worksheets.");
        var rows = ws.RowsUsed().ToList();
        if (rows.Count <= 1)
        {
            result.Errors.Add(new UploadRowError(0, "File", "Excel file is empty or only contains headers."));
            return result;
        }

        // 3. Match headers
        var firstRow = rows[0];
        var headersMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= ws.ColumnsUsed().Count(); col++)
        {
            var headerName = firstRow.Cell(col).Value.ToString().Trim();
            if (!string.IsNullOrEmpty(headerName))
            {
                headersMap[headerName] = col;
            }
        }

        // Validate that mapped headers are present in the Excel file
        foreach (var mapping in template.Mappings)
        {
            if (!headersMap.ContainsKey(mapping.SourceHeader))
            {
                result.Errors.Add(new UploadRowError(1, mapping.SourceHeader, $"Required column header '{mapping.SourceHeader}' was not found in the uploaded file."));
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        // 4. Cache database lookups for validation (e.g. Party codes, Branch codes)
        var validParties = await cache.GetValidPartyCodesAsync(cancellationToken);
        var validBranches = await cache.GetValidBranchCodesAsync(cancellationToken);

        // 5. Parse and validate each row
        for (int rIndex = 1; rIndex < rows.Count; rIndex++)
        {
            var row = rows[rIndex];
            var rowNumber = rIndex + 1; // Excel is 1-indexed, headers are row 1
            var rowData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            bool rowHasErrors = false;

            foreach (var column in template.Columns)
            {
                // Find mapping for this destination column
                var mapping = template.Mappings.FirstOrDefault(m => m.DestinationColumn.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                if (mapping == null)
                    continue;

                if (!headersMap.TryGetValue(mapping.SourceHeader, out int colNum))
                    continue;

                var cell = row.Cell(colNum);
                var rawValue = cell.Value.ToString().Trim();

                // Check required constraint
                if (column.IsRequired && string.IsNullOrEmpty(rawValue))
                {
                    result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Column '{mapping.SourceHeader}' is required."));
                    rowHasErrors = true;
                    continue;
                }

                if (string.IsNullOrEmpty(rawValue))
                {
                    rowData[column.ColumnName] = DBNull.Value;
                    continue;
                }

                // Check datatype and convert
                object parsedValue;
                try
                {
                    parsedValue = column.DataType.ToLowerInvariant() switch
                    {
                        "int" or "integer" => int.Parse(rawValue, CultureInfo.InvariantCulture),
                        "decimal" or "double" or "numeric" => decimal.Parse(rawValue.Replace("%", ""), CultureInfo.InvariantCulture),
                        "datetime" or "date" => DateTime.Parse(rawValue, CultureInfo.InvariantCulture),
                        _ => rawValue
                    };
                    rowData[column.ColumnName] = parsedValue;
                }
                catch (Exception)
                {
                    result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Value '{rawValue}' is not a valid {column.DataType}."));
                    rowHasErrors = true;
                    continue;
                }

                // Run validation rules
                var rules = template.ValidationRules.Where(rule => rule.ColumnName.Equals(column.ColumnName, StringComparison.OrdinalIgnoreCase));
                foreach (var rule in rules)
                {
                    if (rule.ValidationType.Equals("Range", StringComparison.OrdinalIgnoreCase))
                    {
                        if (parsedValue is decimal decVal)
                        {
                            // Example ValidationConfig: ">=0" or "between 0 and 1000"
                            var match = Regex.Match(rule.ValidationConfig, @"(>=|<=|>|<|=)\s*(-?\d+(\.\d+)?)");
                            if (match.Success)
                            {
                                var op = match.Groups[1].Value;
                                var target = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                                bool valid = op switch
                                {
                                    ">=" => decVal >= target,
                                    "<=" => decVal <= target,
                                    ">" => decVal > target,
                                    "<" => decVal < target,
                                    "=" => decVal == target,
                                    _ => true
                                };
                                if (!valid)
                                {
                                    result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Value must be {rule.ValidationConfig}."));
                                    rowHasErrors = true;
                                }
                            }
                        }
                    }
                    else if (rule.ValidationType.Equals("Regex", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Regex.IsMatch(rawValue, rule.ValidationConfig))
                        {
                            result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Value '{rawValue}' does not match pattern."));
                            rowHasErrors = true;
                        }
                    }
                    else if (rule.ValidationType.Equals("DbLookup", StringComparison.OrdinalIgnoreCase))
                    {
                        // Example: "Parties" or "Branches"
                        if (rule.ValidationConfig.Equals("Parties", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!validParties.Contains(rawValue))
                            {
                                result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Party code '{rawValue}' does not exist in the database."));
                                rowHasErrors = true;
                            }
                        }
                        else if (rule.ValidationConfig.Equals("Branches", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!validBranches.Contains(rawValue))
                            {
                                result.Errors.Add(new UploadRowError(rowNumber, mapping.SourceHeader, $"Branch code '{rawValue}' does not exist in the database."));
                                rowHasErrors = true;
                            }
                        }
                    }
                }
            }

            if (!rowHasErrors)
            {
                rowData["RowNumber"] = rowNumber;
                result.Rows.Add(rowData);
            }
        }

        return result;
    }

    public async Task<UploadCommitResult> CommitAsync(Stream fileStream, string templateCode, string username, CancellationToken cancellationToken)
    {
        var preview = await PreviewAsync(fileStream, templateCode, cancellationToken);
        var result = new UploadCommitResult
        {
            TotalRows = preview.Rows.Count + preview.Errors.Select(e => e.RowNumber).Distinct().Count(),
            FailedRows = preview.Errors.Select(e => e.RowNumber).Distinct().Count(),
            Errors = preview.Errors
        };

        if (!preview.IsValid)
        {
            result.Success = false;
            result.Message = $"Commit aborted. Validation failed with {result.FailedRows} row errors.";
            return result;
        }

        var template = await db.ImportTemplates.FirstOrDefaultAsync(t => t.Code == templateCode, cancellationToken);
        if (template == null)
        {
            result.Success = false;
            result.Message = "Template not found.";
            return result;
        }

        // Insert using transaction
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (template.TargetTable.Equals("Raw", StringComparison.OrdinalIgnoreCase))
            {
                // Map dictionaries to RawRecords
                var records = preview.Rows.Select(row => new RawRecord
                {
                    ConsPartyCode = row.GetValueOrDefault("ConsPartyCode")?.ToString() ?? string.Empty,
                    ConsPartyName = row.GetValueOrDefault("ConsPartyName")?.ToString() ?? string.Empty,
                    Loc = row.GetValueOrDefault("Loc")?.ToString() ?? string.Empty,
                    PartCategoryCode = row.GetValueOrDefault("PartCategoryCode")?.ToString() ?? string.Empty,
                    DealerSubType = row.GetValueOrDefault("DealerSubType")?.ToString() ?? string.Empty,
                    Consignee = row.GetValueOrDefault("Consignee")?.ToString() ?? string.Empty,
                    PartyType = row.GetValueOrDefault("PartyType")?.ToString() ?? string.Empty,
                    DocumentNum = row.GetValueOrDefault("DocumentNum")?.ToString() ?? string.Empty,
                    Remarks = row.GetValueOrDefault("Remarks")?.ToString() ?? string.Empty,
                    NetRetailSelling = ConvertToDecimal(row.GetValueOrDefault("NetRetailSelling")),
                    DiscountAmount = ConvertToDecimal(row.GetValueOrDefault("DiscountAmount")),
                    NetRetailDdl = ConvertToDecimal(row.GetValueOrDefault("NetRetailDdl")),
                    OriginalCode = row.GetValueOrDefault("OriginalCode")?.ToString() ?? string.Empty,
                    Month = row.GetValueOrDefault("Month")?.ToString() ?? string.Empty,
                    MonthYear = row.GetValueOrDefault("MonthYear")?.ToString() ?? string.Empty,
                    FiscalYear = row.GetValueOrDefault("FiscalYear")?.ToString() ?? string.Empty,
                    Quarter = row.GetValueOrDefault("Quarter")?.ToString() ?? string.Empty,
                    RowNumber = ConvertToInt(row.GetValueOrDefault("RowNumber")),
                    MonthNumber = ConvertToInt(row.GetValueOrDefault("MonthNumber")),
                    YearNumber = ConvertToInt(row.GetValueOrDefault("YearNumber")),
                    IsDeleted = false,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                }).ToList();

                db.Raws.AddRange(records);
                await db.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new NotSupportedException($"Target table '{template.TargetTable}' is not supported for generic upload.");
            }

            await transaction.CommitAsync(cancellationToken);
            result.Success = true;
            result.SuccessRows = preview.Rows.Count;
            result.Message = $"Successfully committed {result.SuccessRows} rows.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            result.Success = false;
            result.Message = $"Transaction rolled back due to error: {ex.Message}";
        }

        return result;
    }

    private static decimal ConvertToDecimal(object? obj)
    {
        if (obj == null || obj == DBNull.Value)
            return 0m;
        return Convert.ToDecimal(obj, CultureInfo.InvariantCulture);
    }

    private static int ConvertToInt(object? obj)
    {
        if (obj == null || obj == DBNull.Value)
            return 0;
        return Convert.ToInt32(obj, CultureInfo.InvariantCulture);
    }
}

public static class DictionaryExtensions
{
    public static T? GetValueOrDefault<K, T>(this Dictionary<K, object> dict, K key) where K : notnull
    {
        if (dict.TryGetValue(key, out var val) && val is T typedVal)
            return typedVal;
        return default;
    }
}
