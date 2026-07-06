using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IncentivePortal.Helpers;

public static class ImportOutstandingCommand
{
    public static async Task ExecuteAsync(IServiceProvider services)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("==================================================================");
        Console.WriteLine(" DYNAMIC BULK IMPORT: All Outstanding0201.xlsx");
        Console.WriteLine("==================================================================");
        Console.ResetColor();

        string excelPath = @"c:\Users\ACER\Desktop\Incentive Portal\All Outstanding0201.xlsx";
        if (!File.Exists(excelPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Excel file not found at: {excelPath}");
            Console.ResetColor();
            return;
        }

        try
        {
            using var db = services.CreateScope().ServiceProvider.GetRequiredService<IncentiveDbContext>();
            
            Console.WriteLine($"[Info] Opening Excel workbook: {excelPath}");
            using var workbook = new XLWorkbook(excelPath);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? workbook.Worksheets.First();
            Console.WriteLine($"[Info] Selected sheet: '{sheet.Name}'");

            // 1. Dynamic Header Row Scanning
            int headerRowIndex = 1;
            int codeCol = 0;
            int outstandingCol = 0;

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
                    break;
                }
            }

            if (codeCol == 0 || outstandingCol == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[Error] Invalid file structure. Excel must contain a Code/Name and an Outstanding/Balance column.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[OK] Header mapped on Row {headerRowIndex}! Code Col={codeCol}, Outstanding Col={outstandingCol}");
            Console.ResetColor();

            // Fetch all parties (tracked)
            Console.WriteLine("[Info] Loading active dealer registry...");
            var parties = await db.Parties.Include(p => p.Branch).Where(p => !p.IsDeleted).ToListAsync();
            Console.WriteLine($"[Info] Loaded {parties.Count} dealers.");

            // 2. Accumulative Sales Record Registry
            int currentMonth = DateTime.UtcNow.Month;
            int currentYear = DateTime.UtcNow.Year;
            var activeSales = new Dictionary<string, SsIncentive>(StringComparer.OrdinalIgnoreCase);

            // Load existing records for the current month first
            var currentSales = await db.SsIncentives
                .Where(s => s.Year == currentYear && s.Month == currentMonth && !s.IsDeleted)
                .ToListAsync();

            foreach (var sale in currentSales)
            {
                activeSales[sale.PartyCode] = sale;
                // Zero out outstanding balance initially so that we can cleanly sum the new Excel balances!
                sale.Outstanding = 0m;
            }

            // Load latest records from previous months as fallbacks
            var latestSaleIds = await db.SsIncentives
                .AsNoTracking()
                .Where(s => !s.IsDeleted)
                .GroupBy(s => s.PartyCode)
                .Select(g => g.OrderByDescending(s => s.Year).ThenByDescending(s => s.Month).Select(x => x.Id).FirstOrDefault())
                .ToListAsync();

            var latestPriorSales = await db.SsIncentives
                .Where(s => latestSaleIds.Contains(s.Id))
                .ToListAsync();

            foreach (var sale in latestPriorSales)
            {
                if (!activeSales.ContainsKey(sale.PartyCode))
                {
                    activeSales[sale.PartyCode] = sale;
                }
            }

            int updatedCount = 0;
            int unmatchedCount = 0;
            var rows = sheet.RowsUsed().Skip(headerRowIndex).ToList();
            Console.WriteLine($"[Info] Processing {rows.Count} data rows starting from Row {headerRowIndex + 1}...");

            foreach (var row in rows)
            {
                var codeVal = row.Cell(codeCol).Value.ToString().Trim();
                var outstandingStr = row.Cell(outstandingCol).Value.ToString().Replace(",", "").Trim();

                if (string.IsNullOrEmpty(codeVal)) continue;
                if (codeVal.Equals("Total", StringComparison.OrdinalIgnoreCase)) continue;

                // Extract code from parentheses if present (e.g. "OM MOTORS(WRJ0106114)")
                string matchCode = codeVal;
                string matchName = codeVal;
                var parenMatch = Regex.Match(codeVal, @"\(([^)]+)\)");
                if (parenMatch.Success)
                {
                    matchCode = parenMatch.Groups[1].Value.Trim();
                    matchName = codeVal.Substring(0, parenMatch.Index).Trim();
                }

                // Match Party by Code or Name
                var party = parties.FirstOrDefault(p => 
                    p.PartyCode.Equals(matchCode, StringComparison.OrdinalIgnoreCase) ||
                    p.PartyCode.Equals(matchName, StringComparison.OrdinalIgnoreCase) ||
                    p.PartyName.Equals(matchCode, StringComparison.OrdinalIgnoreCase) || 
                    p.PartyName.Equals(matchName, StringComparison.OrdinalIgnoreCase) ||
                    p.PartyName.Contains(matchName, StringComparison.OrdinalIgnoreCase) ||
                    matchName.Contains(p.PartyName, StringComparison.OrdinalIgnoreCase));

                if (party != null)
                {
                    decimal parsedOutstanding = 0m;
                    if (decimal.TryParse(outstandingStr, out var rawBal))
                    {
                        parsedOutstanding = Math.Abs(rawBal);
                    }

                    if (activeSales.TryGetValue(party.PartyCode, out var sale))
                    {
                        if (sale.Year == currentYear && sale.Month == currentMonth)
                        {
                            var oldOutstanding = sale.Outstanding;
                            sale.Outstanding += parsedOutstanding; // Safely accumulate multiple bills
                            
                            Console.ForegroundColor = ConsoleColor.Gray;
                            Console.WriteLine($"  [Accumulate] Dealer: {party.PartyCode} | Added: ₹{parsedOutstanding:N0} | Total: ₹{sale.Outstanding:N0}");
                            Console.ResetColor();
                        }
                        else
                        {
                            // It's a previous month's record, create a new current month record instead of editing history
                            var newSale = new SsIncentive
                            {
                                Month = currentMonth,
                                Year = currentYear,
                                MonthLabel = new DateTime(currentYear, currentMonth, 1).ToString("MMMM yyyy"),
                                PartyCode = party.PartyCode,
                                PartyName = party.PartyName,
                                SourceLocation = party.Branch?.Code ?? "HO",
                                SaleValue = 0,
                                OnBillDiscount = 0,
                                Outstanding = parsedOutstanding,
                                SlabPercent = 0,
                                AchievementPercent = 0,
                                GrossIncentive = 0,
                                TdsAmount = 0,
                                NetTransferAmount = 0,
                                Mode = "Dynamic",
                                Status = "Posted",
                                PaymentStatus = "Pending",
                                ProcessingDate = DateTime.UtcNow,
                                IsDeleted = false
                            };
                            db.SsIncentives.Add(newSale);
                            activeSales[party.PartyCode] = newSale; // Track for subsequent rows

                            Console.ForegroundColor = ConsoleColor.DarkGreen;
                            Console.WriteLine($"  [Create] Dealer: {party.PartyCode} | New Current Period | Set Outstanding: ₹{parsedOutstanding:N0}");
                            Console.ResetColor();
                        }
                    }
                    else
                    {
                        // No sales history at all, create new current month record
                        var newSale = new SsIncentive
                        {
                            Month = currentMonth,
                            Year = currentYear,
                            MonthLabel = new DateTime(currentYear, currentMonth, 1).ToString("MMMM yyyy"),
                            PartyCode = party.PartyCode,
                            PartyName = party.PartyName,
                            SourceLocation = party.Branch?.Code ?? "HO",
                            SaleValue = 0,
                            OnBillDiscount = 0,
                            Outstanding = parsedOutstanding,
                            SlabPercent = 0,
                            AchievementPercent = 0,
                            GrossIncentive = 0,
                            TdsAmount = 0,
                            NetTransferAmount = 0,
                            Mode = "Dynamic",
                            Status = "Posted",
                            PaymentStatus = "Pending",
                            ProcessingDate = DateTime.UtcNow,
                            IsDeleted = false
                        };
                        db.SsIncentives.Add(newSale);
                        activeSales[party.PartyCode] = newSale; // Track for subsequent rows

                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"  [Create-New] Dealer: {party.PartyCode} | Set Outstanding: ₹{parsedOutstanding:N0}");
                        Console.ResetColor();
                    }

                    party.UpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
                else
                {
                    unmatchedCount++;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [Warning] Row {row.RowNumber()} | Match failed for '{codeVal}' (Code: '{matchCode}', Name: '{matchName}') | Amt: ₹{outstandingStr}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("[Info] Committing outstanding balances to SQL Database...");
            await db.SaveChangesAsync();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("==================================================================");
            Console.WriteLine($" SUCCESS: Bulk import completed successfully!");
            Console.WriteLine($"   - Updated/Synced Dealers: {updatedCount}");
            Console.WriteLine($"   - Unmatched Spreadsheet Rows: {unmatchedCount}");
            Console.WriteLine("==================================================================");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] Import failed with exception: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {ex.InnerException.Message}");
            }
            Console.ResetColor();
        }
    }
}
