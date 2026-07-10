using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IncentivePortal.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Application.Reports;

public sealed class DynamicReportService(IncentiveDbContext db) : IDynamicReportService
{
    private static readonly HashSet<string> AllowedDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "DealerSubType", "Consignee", "DealerCode", "Loc", "PartCategoryCode",
        "FiscalYear", "Quarter", "Month", "MonthYear", "ConsPartyCode", "ConsPartyName",
        "PartyType", "DocumentNum", "Remarks", "OriginalCode", "PartNum", "RootPartNum",
        "Day", "MonthNumber", "YearNumber"
    };

    private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "NetRetailSelling", "DiscountAmount", "NetRetailDdl", "AchievementPercent", "NetRetailQty"
    };

    public async Task<object> QueryReportDataAsync(
        string reportType, 
        List<string> dimensions, 
        List<PivotValueMetric> values, 
        List<FilterCriterion> filters, 
        List<SortCriterion> sorts, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default)
    {
        var (sql, countSql, parameters) = BuildSqlQueries(reportType, dimensions, values, filters, sorts, page, pageSize);

        var data = new List<Dictionary<string, object>>();
        int totalCount = 0;

        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(cancellationToken);

        try
        {
            // Execute count first
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = countSql;
                foreach (var p in parameters)
                {
                    var param = countCmd.CreateParameter();
                    param.ParameterName = p.ParameterName;
                    param.Value = p.Value ?? DBNull.Value;
                    countCmd.Parameters.Add(param);
                }
                var countResult = await countCmd.ExecuteScalarAsync(cancellationToken);
                totalCount = Convert.ToInt32(countResult);
            }

            // Execute data query
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var p in parameters)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = p.ParameterName;
                    param.Value = p.Value ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }

                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                {
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.IsDBNull(i) ? null! : reader.GetValue(i);
                        }
                        data.Add(row);
                    }
                }
            }
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        return new
        {
            data,
            totalCount,
            page,
            pageSize
        };
    }

    public async Task<byte[]> ExportReportDataAsync(
        string format, 
        string reportType, 
        List<string> dimensions, 
        List<PivotValueMetric> values, 
        List<FilterCriterion> filters, 
        List<SortCriterion> sorts, 
        CancellationToken cancellationToken = default)
    {
        // For export, we request page 1 with a large size to retrieve all records
        var resultObj = await QueryReportDataAsync(reportType, dimensions, values, filters, sorts, 1, 100000, cancellationToken);
        var dynamicResult = (dynamic)resultObj;
        var dataList = (List<Dictionary<string, object>>)dynamicResult.data;

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            return GenerateCsv(dataList);
        }

        // Default fallback to CSV for ease of integration
        return GenerateCsv(dataList);
    }

    private byte[] GenerateCsv(List<Dictionary<string, object>> data)
    {
        if (data == null || data.Count == 0) return Array.Empty<byte>();

        var sb = new StringBuilder();
        var headers = data[0].Keys.ToList();
        sb.AppendLine(string.Join(",", headers.Select(h => $"\"{h.Replace("\"", "\"\"")}\"")));

        foreach (var row in data)
        {
            var line = headers.Select(h =>
            {
                var val = row.TryGetValue(h, out var v) ? v : null;
                if (val == null) return "\"\"";
                var str = val.ToString() ?? "";
                return $"\"{str.Replace("\"", "\"\"")}\"";
            });
            sb.AppendLine(string.Join(",", line));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private (string Sql, string CountSql, List<SqlParameter> Parameters) BuildSqlQueries(
        string reportType,
        List<string> dimensions,
        List<PivotValueMetric> values,
        List<FilterCriterion> filters,
        List<SortCriterion> sorts,
        int page,
        int pageSize)
    {
        var validDims = dimensions.Where(d => AllowedDimensions.Contains(d)).ToList();
        var validVals = values.Where(v => AllowedValues.Contains(v.Field)).ToList();

        var selectBuilder = new StringBuilder();
        var groupByBuilder = new StringBuilder();
        var whereBuilder = new StringBuilder("IsDeleted = 0 AND ImportLogId > 0");
        var parameters = new List<SqlParameter>();
        int paramCounter = 0;

        // Apply filters safely
        if (filters != null)
        {
            foreach (var filter in filters)
            {
                if (string.IsNullOrEmpty(filter.Field) || filter.Values == null || filter.Values.Count == 0) continue;
                
                string cleanField = "";
                if (AllowedDimensions.Contains(filter.Field))
                    cleanField = filter.Field;
                else if (AllowedValues.Contains(filter.Field))
                    cleanField = filter.Field;
                else
                    continue;

                if (filter.Operator.Equals("contains", StringComparison.OrdinalIgnoreCase))
                {
                    var paramName = $"@p{paramCounter++}";
                    whereBuilder.Append($" AND [{cleanField}] LIKE {paramName}");
                    parameters.Add(new SqlParameter(paramName, $"%{filter.Values[0]}%"));
                }
                else
                {
                    var paramNames = new List<string>();
                    foreach (var val in filter.Values)
                    {
                        var paramName = $"@p{paramCounter++}";
                        paramNames.Add(paramName);
                        parameters.Add(new SqlParameter(paramName, val));
                    }
                    whereBuilder.Append($" AND [{cleanField}] IN ({string.Join(",", paramNames)})");
                }
            }
        }

        string sqlQuery = "";
        string countQuery = "";

        bool isPivot = reportType.Equals("Pivot", StringComparison.OrdinalIgnoreCase) || reportType.Equals("Summary", StringComparison.OrdinalIgnoreCase);

        if (isPivot && validDims.Count > 0)
        {
            // Build SELECT for dimensions
            selectBuilder.Append(string.Join(", ", validDims.Select(d => $"[{d}]")));
            groupByBuilder.Append(string.Join(", ", validDims.Select(d => $"[{d}]")));

            // Build SELECT for aggregates
            if (validVals.Count > 0)
            {
                foreach (var val in validVals)
                {
                    string aggFunc = val.Aggregation.ToUpperInvariant() switch
                    {
                        "SUM" => "SUM",
                        "COUNT" => "COUNT",
                        "AVG" => "AVG",
                        "MIN" => "MIN",
                        "MAX" => "MAX",
                        _ => "SUM"
                    };
                    selectBuilder.Append($", {aggFunc}([{val.Field}]) AS [{aggFunc}_{val.Field}]");
                }
            }
            else
            {
                selectBuilder.Append(", COUNT(*) AS [RecordCount]");
            }

            // Build ORDER BY
            var orderByBuilder = new StringBuilder();
            if (sorts != null && sorts.Count > 0)
            {
                var validSorts = new List<string>();
                foreach (var s in sorts)
                {
                    if (validDims.Contains(s.Field))
                    {
                        validSorts.Add($"[{s.Field}] {(s.Descending ? "DESC" : "ASC")}");
                    }
                    else if (validVals.Any(v => v.Field.Equals(s.Field, StringComparison.OrdinalIgnoreCase)))
                    {
                        var metric = validVals.First(v => v.Field.Equals(s.Field, StringComparison.OrdinalIgnoreCase));
                        string aggName = metric.Aggregation.ToUpperInvariant() switch
                        {
                            "SUM" => "SUM", "COUNT" => "COUNT", "AVG" => "AVG", "MIN" => "MIN", "MAX" => "MAX", _ => "SUM"
                        };
                        validSorts.Add($"[{aggName}_{metric.Field}] {(s.Descending ? "DESC" : "ASC")}");
                    }
                }
                if (validSorts.Count > 0)
                    orderByBuilder.Append(string.Join(", ", validSorts));
            }
            if (orderByBuilder.Length == 0)
            {
                orderByBuilder.Append($"[{validDims[0]}] ASC");
            }

            sqlQuery = $@"
                SELECT {selectBuilder}
                FROM [Raw]
                WHERE {whereBuilder}
                GROUP BY {groupByBuilder}
                ORDER BY {orderByBuilder}
                OFFSET {(page - 1) * pageSize} ROWS FETCH NEXT pageSize ROWS ONLY";

            // Offset pagination helper replacement
            sqlQuery = sqlQuery.Replace("pageSize", pageSize.ToString());

            countQuery = $@"
                SELECT COUNT(*) FROM (
                    SELECT 1 AS [Dummy]
                    FROM [Raw]
                    WHERE {whereBuilder}
                    GROUP BY {groupByBuilder}
                ) AS [SubQuery]";
        }
        else
        {
            // Default Detail Tabular Report
            var selectCols = validDims.Count > 0 ? validDims : AllowedDimensions.ToList();
            var selectStr = string.Join(", ", selectCols.Select(c => $"[{c}]"));
            
            if (validVals.Count > 0)
            {
                selectStr += ", " + string.Join(", ", validVals.Select(v => $"[{v.Field}]"));
            }
            else
            {
                selectStr += ", [NetRetailSelling], [DiscountAmount]";
            }

            var orderByBuilder = new StringBuilder();
            if (sorts != null && sorts.Count > 0)
            {
                var validSorts = sorts
                    .Where(s => AllowedDimensions.Contains(s.Field) || AllowedValues.Contains(s.Field))
                    .Select(s => $"[{s.Field}] {(s.Descending ? "DESC" : "ASC")}")
                    .ToList();
                if (validSorts.Count > 0)
                    orderByBuilder.Append(string.Join(", ", validSorts));
            }
            if (orderByBuilder.Length == 0)
            {
                orderByBuilder.Append("[Id] DESC");
            }

            sqlQuery = $@"
                SELECT {selectStr}
                FROM [Raw]
                WHERE {whereBuilder}
                ORDER BY {orderByBuilder}
                OFFSET {(page - 1) * pageSize} ROWS FETCH NEXT pageSize ROWS ONLY";

            sqlQuery = sqlQuery.Replace("pageSize", pageSize.ToString());

            countQuery = $"SELECT COUNT(*) FROM [Raw] WHERE {whereBuilder}";
        }

        return (sqlQuery, countQuery, parameters);
    }
}
