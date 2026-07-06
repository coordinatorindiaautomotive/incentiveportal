using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Services;
using IncentivePortal.Data;

namespace IncentivePortal.Helpers;

public static class ImportLastYearCommand
{
    public static async Task ExecuteAsync(IServiceProvider services, string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine(" LAST YEAR IMPORT COMMAND");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        string excelPath = @"c:\Users\ACER\Desktop\Incentive Portal\Last Year 2025-26.xlsx";
        if (!File.Exists(excelPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Excel file not found at: {excelPath}");
            Console.ResetColor();
            return;
        }

        bool inspectOnly = args.Contains("--import-last-year-inspect");

        try
        {
            Console.WriteLine($"[Info] Opening Excel workbook: {excelPath} (might take a few seconds...)");
            using var workbook = new XLWorkbook(excelPath);
            Console.WriteLine($"[Info] Worksheets found in workbook: {workbook.Worksheets.Count}");
            foreach (var ws in workbook.Worksheets)
            {
                Console.WriteLine($" - Sheet: '{ws.Name}', Rows: {ws.RowsUsed().Count()}");
            }

            var sheet = workbook.Worksheets.FirstOrDefault(x => x.Name.Equals("Summary", StringComparison.OrdinalIgnoreCase)) ?? workbook.Worksheets.First();
            Console.WriteLine($"[Info] Using sheet: '{sheet.Name}' for inspection.");

            // Print first row (headers)
            var firstRow = sheet.Row(1);
            var headers = firstRow.CellsUsed().Select(c => c.Value.ToString().Trim()).ToList();
            Console.WriteLine($"[Info] Headers: {string.Join(" | ", headers)}");

            if (args.Contains("--sum-excel"))
            {
                Console.WriteLine("[Info] Running sum inspection directly on Excel sheet...");
                var colMap = firstRow.CellsUsed().ToDictionary(
                    c => c.Value.ToString().Trim(),
                    c => c.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase
                );

                decimal totalNetRetail = 0m;
                int matchCount = 0;
                
                var dataRows = sheet.RowsUsed().Skip(1);
                foreach (var row in dataRows)
                {
                    var my = row.Cell(colMap["Month Year"]).Value.ToString().Trim();
                    var loc = row.Cell(colMap["Loc"]).Value.ToString().Trim();
                    var cat = row.Cell(colMap["Part Category Code"]).Value.ToString().Trim();
                    var type = row.Cell(colMap["Party Type"]).Value.ToString().Trim();
                    
                    if (my.Equals("May 2025", StringComparison.OrdinalIgnoreCase) &&
                        loc.Equals("GRL", StringComparison.OrdinalIgnoreCase) &&
                        cat.Equals("M", StringComparison.OrdinalIgnoreCase) &&
                        type.Equals("INDEPENDENT WORKSHOP", StringComparison.OrdinalIgnoreCase))
                    {
                        var valStr = row.Cell(colMap["Net Retail Selling"]).Value.ToString().Replace(",", "").Trim();
                        if (decimal.TryParse(valStr, out decimal val))
                        {
                            totalNetRetail += val;
                            matchCount++;
                        }
                    }
                }
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[RESULT] Sum of 'Net Retail Selling' for May 2025 GRL Category M INDEPENDENT WORKSHOP in Excel: {totalNetRetail} (Rows: {matchCount})");
                Console.ResetColor();
                return;
            }

            if (inspectOnly)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[OK] Inspection completed successfully!");
                Console.ResetColor();
                return;
            }

            // Perform actual import using ISalesImportService
            using var scope = services.CreateScope();
            var importService = scope.ServiceProvider.GetRequiredService<ISalesImportService>();
            var db = scope.ServiceProvider.GetRequiredService<IncentiveDbContext>();
            var calculationService = scope.ServiceProvider.GetRequiredService<IIncentiveCalculationService>();

            Console.WriteLine("[Info] Constructing mock FormFile...");
            using var fileStream = File.OpenRead(excelPath);
            var mockFile = new FormFile(fileStream, 0, fileStream.Length, "file", Path.GetFileName(excelPath))
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            };

            Console.WriteLine("[Info] Running importService.PreviewAsync in Historical mode...");
            var previewRows = await importService.PreviewAsync(mockFile, "Historical");
            Console.WriteLine($"[Info] Preview parsed {previewRows.Count} rows.");

            var groups = previewRows
                .GroupBy(x => new { x.Year, x.Month })
                .Select(g => new { Period = $"{g.Key.Month}/{g.Key.Year}", Count = g.Count(), Year = g.Key.Year, Month = g.Key.Month })
                .OrderBy(x => x.Year).ThenBy(x => x.Month)
                .ToList();
            Console.WriteLine("[Info] Unique periods found in spreadsheet preview:");
            foreach (var g in groups)
            {
                Console.WriteLine($"  - Period: {g.Period}, Consolidated Rows: {g.Count}");
            }

            // We filter specifically to May 2025 for this targeted fix
            var targetRows = previewRows.Where(x => x.Year == 2025 && x.Month == 5).ToList();
            Console.WriteLine($"[Info] Filtered to May 2025: {targetRows.Count} rows for targeted import.");

            var errorRows = targetRows.Where(x => !string.IsNullOrEmpty(x.Error)).ToList();
            if (errorRows.Count > 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[Warning] Found {errorRows.Count} target rows with errors. Showing top 5:");
                foreach (var err in errorRows.Take(5))
                {
                    Console.WriteLine($"  - Party: {err.PartyCode}, Error: {err.Error}");
                }
                Console.ResetColor();
            }

            Console.WriteLine("[Info] Committing records using importService.CommitAsync in Historical mode...");
            var summary = await importService.CommitAsync(
                targetRows,
                Path.GetFileName(excelPath),
                "Historical",
                "Command line automated May 2025 targeted data import for Category Sales fix",
                null,
                cancellationToken: default,
                file: mockFile);

            var log = summary.Log;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] Import committed successfully! Log ID: {log.Id}");
            Console.WriteLine($"  - Total Rows: {summary.TotalRows}");
            Console.WriteLine($"  - Success Rows: {summary.Committed}");
            Console.WriteLine($"  - Failed Rows: {summary.Skipped}");
            Console.ResetColor();

            // Calculate May 2025
            Console.WriteLine("[Info] Triggering recalculation for May 2025...");
            
            // Check if month lock exists for May 2025 and unlock it temporarily if needed
            var monthLock = await db.MonthLocks.FirstOrDefaultAsync(x => x.LockYear == 2025 && x.LockMonth == 5);
            bool wasLocked = false;
            if (monthLock != null && monthLock.IsLocked)
            {
                wasLocked = true;
                Console.WriteLine("[Info] May 2025 is currently locked. Unlocking temporarily for recalculation...");
                monthLock.IsLocked = false;
                await db.SaveChangesAsync();
            }

            var calcResults = await calculationService.CalculateMonthAsync(5, 2025, forceRecalculate: true);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] May 2025 calculated! Total computed calculations: {calcResults.Count}");
            Console.ResetColor();

            if (wasLocked && monthLock != null)
            {
                Console.WriteLine("[Info] Restoring May 2025 month lock...");
                monthLock.IsLocked = true;
                await db.SaveChangesAsync();
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================================");
            Console.WriteLine(" SUCCESS: Last year data imported and May 2025 recalculated!");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Operation failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
                if (ex.InnerException.InnerException != null)
                {
                    Console.WriteLine($"  Sub-Inner Exception: {ex.InnerException.InnerException.Message}");
                }
            }
            Console.ResetColor();
        }
    }
}
