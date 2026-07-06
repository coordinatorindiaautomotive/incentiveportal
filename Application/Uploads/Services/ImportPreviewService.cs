using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ExcelDataReader;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IImportPreviewService
{
    Task<IReadOnlyList<SalesImportRow>> PreviewAsync(IFormFile file, string? uploadMode = null, string? branchRulesJson = null, string? alternateCodesJson = null, CancellationToken cancellationToken = default, int? limit = null);
}

public sealed class ImportPreviewService(
    IncentiveDbContext db,
    IImportValidationService validationService
) : IImportPreviewService
{
    // ─── FAST EXCEL READER ─────────────────────────────────────────────────────
    private static System.Data.DataSet ReadWorkbookFast(Stream stream, string fileName)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        IExcelDataReader reader;
        if (ext == ".xls")
            reader = ExcelReaderFactory.CreateBinaryReader(stream);
        else
            reader = ExcelReaderFactory.CreateOpenXmlReader(stream);

        using (reader)
        {
            return reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
            });
        }
    }

    private static IExcelDataReader CreateReader(Stream stream, string fileName)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (ext == ".xls")
            return ExcelReaderFactory.CreateBinaryReader(stream);
        else
            return ExcelReaderFactory.CreateOpenXmlReader(stream);
    }

    private static void ValidateSalesHeadersFromHeaders(HashSet<string> normalizedSheetHeaders)
    {
        var requiredNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "consignee", "dealercode", "loc", "partcategorycode", "fiscalyear",
            "month", "monthyear", "conspartycode", "conspartyname", "partytype",
            "netretailselling", "discountamount"
        };

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["consignee"] = "Consignee",
            ["dealercode"] = "Dealer Code",
            ["loc"] = "Loc",
            ["partcategorycode"] = "Part Category Code",
            ["fiscalyear"] = "Fiscal Year",
            ["month"] = "Month",
            ["monthyear"] = "Month Year",
            ["conspartycode"] = "Cons Party Code",
            ["conspartyname"] = "Cons Party Name",
            ["partytype"] = "Party Type",
            ["netretailselling"] = "Net Retail Selling",
            ["discountamount"] = "Discount Amount"
        };

        var missing = requiredNormalized
            .Where(req => !normalizedSheetHeaders.Contains(req))
            .Select(req => displayNames[req])
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException($"Uploaded Excel sheet structure is invalid. Missing required column(s): {string.Join(", ", missing)}");
        }
    }

    private static void ValidatePreCalculatedHeadersFromHeaders(HashSet<string> normalizedSheetHeaders)
    {
        var expectedNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "month", "conspartycode", "conspartyname", "location", "netretailselling", "discountamount", "slab", "incentive"
        };

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["month"]            = "Month",
            ["conspartycode"]    = "Cons Party Code",
            ["conspartyname"]    = "Cons Party Name",
            ["location"]         = "Location",
            ["netretailselling"] = "Net Retail Selling",
            ["discountamount"]   = "Discount Amount",
            ["slab"]             = "Slab",
            ["incentive"]        = "Incentive"
        };

        var missing = expectedNormalized
            .Where(req => !normalizedSheetHeaders.Contains(req))
            .Select(req => displayNames[req])
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Uploaded Excel sheet structure is invalid. " +
                $"Missing required column(s): {string.Join(", ", missing)}");
        }
    }

    private static string ReaderStr(IExcelDataReader reader, int colIdx)
        => colIdx < 0 || colIdx >= reader.FieldCount ? string.Empty
            : (reader.GetValue(colIdx)?.ToString()?.Trim() ?? string.Empty);

    private static decimal ReaderDec(IExcelDataReader reader, int colIdx)
    {
        if (colIdx < 0 || colIdx >= reader.FieldCount) return 0m;
        var v = reader.GetValue(colIdx);
        if (v == null || v == DBNull.Value) return 0m;
        if (v is double d) return (decimal)d;
        if (v is decimal dec) return dec;
        if (decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0m;
    }

    private static decimal CellPctFromReader(IExcelDataReader reader, int colIdx)
    {
        var v = ReaderDec(reader, colIdx);
        return v > 1m ? v / 100m : v;
    }

    private static Dictionary<string, int> BuildDynamicColumnMapFromHeaders(Dictionary<string, int> headers, IReadOnlyList<ColumnMappingRule> dbMappings)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in headers)
        {
            var raw = kvp.Key.Trim();
            string mappedField = "";
            
            var customMap = dbMappings.FirstOrDefault(m => raw.Equals(m.ExcelHeader, StringComparison.OrdinalIgnoreCase));
            if (customMap != null)
            {
                mappedField = customMap.PortalField;
            }
            else if (_headerAliases.TryGetValue(raw, out var alias))
            {
                mappedField = alias;
            }
            else
            {
                mappedField = raw;
            }

            var key = _fieldToKey.TryGetValue(mappedField, out var k) ? k : mappedField;
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = kvp.Value;
        }
        return map;
    }

    private static (int Month, int Year) ResolvePeriodStreaming(
        IExcelDataReader reader,
        string targetSheetName,
        string fileName,
        Dictionary<string, int> columns)
    {
        if (TryParsePeriod(fileName, out var period))
            return period;

        try
        {
            if (targetSheetName.Equals("Raw", StringComparison.OrdinalIgnoreCase))
            {
                if (reader.FieldCount >= 3)
                {
                    var val = reader.GetValue(2)?.ToString();
                    if (TryParsePeriod(val, out period))
                        return period;
                }
            }
            else
            {
                columns.TryGetValue("Month Year", out int colMonthYear);
                columns.TryGetValue("Month", out int colMonth);
                columns.TryGetValue("Year", out int colYear);
                columns.TryGetValue("Fiscal Year", out int colFY);

                if (colMonthYear >= 0 && colMonthYear < reader.FieldCount)
                {
                    var val = reader.GetValue(colMonthYear)?.ToString();
                    if (TryParsePeriod(val, out period))
                        return period;
                }

                if (colMonth >= 0 && colMonth < reader.FieldCount && (colYear >= 0 || colFY >= 0))
                {
                    var mText = reader.GetValue(colMonth)?.ToString();
                    int yCol = colYear >= 0 ? colYear : colFY;
                    var yText = reader.GetValue(yCol)?.ToString();

                    int month = 0;
                    if (int.TryParse(mText, out int mInt) && mInt >= 1 && mInt <= 12)
                    {
                        month = mInt;
                    }
                    else if (!string.IsNullOrEmpty(mText))
                    {
                        var match = Regex.Match(mText, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            month = DateTime.ParseExact(match.Value, "MMM", CultureInfo.InvariantCulture).Month;
                        }
                    }

                    int year = 0;
                    if (!string.IsNullOrEmpty(yText))
                    {
                        var yearMatch = Regex.Match(yText, @"\d{2,4}");
                        if (yearMatch.Success)
                        {
                            year = int.Parse(yearMatch.Value, CultureInfo.InvariantCulture);
                            if (year < 100) year += 2000;
                        }
                    }

                    if (month >= 1 && month <= 12 && year > 0)
                    {
                        return (month, year);
                    }
                }
            }
        }
        catch {}

        return (DateTime.UtcNow.Month, DateTime.UtcNow.Year);
    }

    private static readonly Dictionary<string, string> _headerAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Incentive "] = "Incentive",
        ["Gross Incentive"] = "Incentive",
        ["Incentive Amount"] = "Incentive",
        ["Slab Applied"] = "Slab",
        ["Slab %"] = "Slab",
        ["Slab Rate"] = "Slab",
        ["Achievement %"] = "Achievement Percent",
        ["AchievementPercent"] = "Achievement Percent",
        ["Loc"] = "Location",
        ["Branch"] = "Location",
        ["Net Retail (Sales)"] = "Net Retail Selling",
        ["Net Retail"] = "Net Retail Selling",
        ["Party Code"] = "Cons Party Code",
        ["Party Name"] = "Cons Party Name",
        ["Transfer Amount"] = "Transfer Amount",
        ["Payout Amount"] = "Transfer Amount",
        ["Net Transfer Amount"] = "Transfer Amount",
        ["Net Transfer"] = "Transfer Amount",
        ["Transfer Eligible"] = "Transfer Amount",
        ["Eligible for Incentive"] = "Transfer Amount",
        ["EligibleForIncentive"] = "Transfer Amount",
        ["TransferEligible"] = "Transfer Amount",
        ["Payment Status"] = "Payment Status",
        ["Status"] = "Payment Status",
        ["UTR"] = "UTR",
        ["UTR Number"] = "UTR",
        ["UTRNumber"] = "UTR",
        ["Payment Date"] = "Payment Date",
        ["Date"] = "Payment Date",
        ["PaymentDate"] = "Payment Date",
        ["Beneficiary Name"] = "Beneficiary Name",
        ["Benefiacaly Name"] = "Beneficiary Name",
        ["BeneficiaryName"] = "Beneficiary Name",
        ["Bank Account Number"] = "Bank Account Number",
        ["Account No"] = "Bank Account Number",
        ["AccountNumber"] = "Bank Account Number",
        ["BankAccountNumber"] = "Bank Account Number",
        ["IFSC"] = "IFSC",
        ["IFSC Code"] = "IFSC",
        ["Part Category Co"] = "Part Category Code",
        ["Part Category Coc"] = "Part Category Code",
        ["Part Category Cod"] = "Part Category Code",
        ["Part Num"] = "Part Num",
        ["Root Part Num"] = "Root Part Num",
        ["Net Retail Qty"] = "Net Retail Qty"
    };

    private static readonly Dictionary<string, string> _fieldToKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Location"] = "Location",
        ["Loc"] = "Location",
        ["Branch"] = "Location",
        ["PartCategoryCode"] = "Part Category Code",
        ["Part Category Code"] = "Part Category Code",
        ["Part Category Co"] = "Part Category Code",
        ["Part Category Coc"] = "Part Category Code",
        ["Part Category Cod"] = "Part Category Code",
        ["Part Num"] = "Part Num",
        ["Root Part Num"] = "Root Part Num",
        ["Net Retail Qty"] = "Net Retail Qty",
        ["DealerType"] = "Party Type",
        ["PartyType"] = "Party Type",
        ["Party Type"] = "Party Type",
        ["DealerSubType"] = "Dealer Sub-Type",
        ["Dealer Sub-Type"] = "Dealer Sub-Type",
        ["DealerCode"] = "Dealer Code",
        ["Dealer Code"] = "Dealer Code",
        ["Consignee"] = "Consignee",
        ["FiscalYear"] = "Fiscal Year",
        ["Fiscal Year"] = "Fiscal Year",
        ["Quarter"] = "Quarter",
        ["DocumentNum"] = "Document Num",
        ["Document Num"] = "Document Num",
        ["Remarks"] = "Remarks",
        ["NetRetailDdl"] = "Net Retail DDL",
        ["Net Retail DDL"] = "Net Retail DDL",
        ["Net Retail Ddl"] = "Net Retail DDL",
        ["Day"] = "Day",
        ["MonthYear"] = "Month Year",
        ["Month Year"] = "Month Year",
        ["Month"] = "Month",
        ["Year"] = "Year",
        ["PartyCode"] = "Cons Party Code",
        ["ConsPartyCode"] = "Cons Party Code",
        ["Cons Party Code"] = "Cons Party Code",
        ["PartyName"] = "Cons Party Name",
        ["ConsPartyName"] = "Cons Party Name",
        ["Cons Party Name"] = "Cons Party Name",
        ["NetRetailSelling"] = "Net Retail Selling",
        ["SaleValue"] = "Net Retail Selling",
        ["Net Retail Selling"] = "Net Retail Selling",
        ["DiscountAmount"] = "Discount Amount",
        ["Discount"] = "Discount Amount",
        ["Discount Amount"] = "Discount Amount",
        ["Slab"] = "Slab",
        ["Incentive"] = "Incentive",
        ["AchievementPercent"] = "Achievement Percent",
        ["Achievement Percent"] = "Achievement Percent",
        ["Transfer Amount"] = "Transfer Amount",
        ["Payment Status"] = "Payment Status",
        ["UTR"] = "UTR",
        ["Payment Date"] = "Payment Date",
        ["Beneficiary Name"] = "Beneficiary Name",
        ["Bank Account Number"] = "Bank Account Number",
        ["IFSC"] = "IFSC"
    };

    private static Dictionary<string, int> BuildDynamicColumnMap(System.Data.DataTable dt, IReadOnlyList<ColumnMappingRule> dbMappings)
    {
        var customHeaderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in dbMappings)
        {
            if (!string.IsNullOrWhiteSpace(m.ExcelHeader) && !string.IsNullOrWhiteSpace(m.PortalField))
            {
                customHeaderMap[m.ExcelHeader.Trim()] = m.PortalField.Trim();
            }
        }

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var raw = (dt.Columns[c].ColumnName ?? string.Empty).Trim();
            string? mappedField = null;
            if (customHeaderMap.TryGetValue(raw, out var field))
            {
                mappedField = field;
            }
            else if (_headerAliases.TryGetValue(raw, out var alias))
            {
                mappedField = alias;
            }
            else
            {
                mappedField = raw;
            }

            var key = _fieldToKey.TryGetValue(mappedField, out var k) ? k : mappedField;
            if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                map[key] = c;
        }
        return map;
    }

    private static string CellStr(System.Data.DataRow row, int colIdx)
        => colIdx < 0 || colIdx >= row.Table.Columns.Count ? string.Empty
            : (row[colIdx]?.ToString()?.Trim() ?? string.Empty);

    private static decimal CellDec(System.Data.DataRow row, int colIdx)
    {
        if (colIdx < 0 || colIdx >= row.Table.Columns.Count) return 0m;
        var v = row[colIdx];
        if (v == null || v == DBNull.Value) return 0m;
        if (v is double d) return (decimal)d;
        if (v is decimal dec) return dec;
        if (decimal.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return 0m;
    }

    private static decimal CellPct(System.Data.DataRow row, int colIdx)
    {
        var v = CellDec(row, colIdx);
        return v > 1m ? v / 100m : v;
    }

    public async Task<IReadOnlyList<SalesImportRow>> PreviewAsync(IFormFile file, string? uploadMode = null, string? branchRulesJson = null, string? alternateCodesJson = null, CancellationToken cancellationToken = default, int? limit = null)
    {
        var dbMappings = await db.ColumnMappingRules
            .Where(x => x.IsActive && !x.IsDeleted && x.UploadContext == "MonthlySales")
            .ToListAsync(cancellationToken);

        // ── STEP 1: Fast binary read of sheet names ──────────────────────
        string targetSheetName = "";
        using (var checkStream = file.OpenReadStream())
        using (var checkReader = CreateReader(checkStream, file.FileName))
        {
            var sheetNames = new List<string>();
            do
            {
                sheetNames.Add(checkReader.Name);
            } while (checkReader.NextResult());

            if (sheetNames.Contains("Raw", StringComparer.OrdinalIgnoreCase))
                targetSheetName = "Raw";
            else if (sheetNames.Contains("Summary", StringComparer.OrdinalIgnoreCase))
                targetSheetName = "Summary";
            else
                targetSheetName = sheetNames.FirstOrDefault() ?? "";
        }

        if (string.IsNullOrEmpty(targetSheetName))
            throw new InvalidOperationException("Uploaded workbook contains no spreadsheets.");

        // ── STEP 2: Stream target sheet row by row ──────────────────────────
        using var stream = file.OpenReadStream();
        using var reader = CreateReader(stream, file.FileName);

        // Navigate to the target sheet
        while (reader.Name != targetSheetName)
        {
            if (!reader.NextResult())
                break;
        }

        // Read header row
        if (!reader.Read())
            throw new InvalidOperationException($"The sheet '{targetSheetName}' is empty.");

        var headerColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var normalizedSheetHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int c = 0; c < reader.FieldCount; c++)
        {
            var headerVal = reader.GetValue(c)?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(headerVal))
            {
                headerColumns[headerVal] = c;
                var norm = NormalizeHeader(headerVal);
                if (norm == "partcategoryco" || norm == "partcategorycoc" || norm == "partcategorycod")
                {
                    norm = "partcategorycode";
                }
                normalizedSheetHeaders.Add(norm);
            }
        }

        // Validate headers
        if (uploadMode == "Sales")
        {
            ValidateSalesHeadersFromHeaders(normalizedSheetHeaders);
        }
        else if (uploadMode == "PreCalculated")
        {
            ValidatePreCalculatedHeadersFromHeaders(normalizedSheetHeaders);
        }
        else
        {
            // Validate all columns recognized
            var unrecognizedColumns = new List<string>();
            foreach (var kvp in headerColumns)
            {
                if (!IsHeaderRecognized(kvp.Key, dbMappings))
                {
                    unrecognizedColumns.Add(kvp.Key);
                }
            }
            if (unrecognizedColumns.Count > 0)
            {
                throw new InvalidOperationException($"Uploaded Excel sheet structure is invalid. Unrecognized/invalid column header(s): {string.Join(", ", unrecognizedColumns)}");
            }
        }

        var columns = BuildDynamicColumnMapFromHeaders(headerColumns, dbMappings);

        var hasSlab = columns.ContainsKey("Slab");
        var hasIncentive = columns.ContainsKey("Incentive");
        var hasAchievement = columns.ContainsKey("Achievement Percent");

        // Read first data row to determine period, then we will parse it.
        if (!reader.Read())
        {
            throw new InvalidOperationException($"The sheet '{targetSheetName}' has no data rows.");
        }

        // Now resolve period using reader (which is at the first data row)
        var period = ResolvePeriodStreaming(reader, targetSheetName, file.FileName, columns);

        // ── STEP 4: Month lock check ─────────────────────────────────────────
        var isHistorical = uploadMode == "Historical" || (uploadMode == "Sales" && period.Year < DateTime.Today.Year);
        var isLocked = await db.MonthLocks.AnyAsync(x => x.LockYear == period.Year && (x.LockMonth == period.Month || x.LockMonth == 0) && x.IsLocked, cancellationToken);
        if (isLocked && !isHistorical)
            throw new InvalidOperationException($"The selected period {period.Month}/{period.Year} is locked. Re-upload is only possible via authorized correction.");

        // ── STEP 5: Load incentive scheme ────────────────────────────────────
        var calcDate = new DateTime(period.Year, period.Month, 1);
        var scheme = await db.IncentiveSchemes.Include(x => x.Details)
            .Where(x => x.Name != "Imported Workbook Scheme" && x.EffectiveFrom <= calcDate && x.EffectiveTo >= calcDate)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => x.Name != "Imported Workbook Scheme" && x.EffectiveFrom <= calcDate)
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken)
            ?? await db.IncentiveSchemes.Include(x => x.Details)
                .Where(x => x.Name != "Imported Workbook Scheme")
                .OrderBy(x => x.EffectiveFrom).ThenByDescending(x => x.Version)
                .FirstOrDefaultAsync(cancellationToken);

        // ── STEP 6: Load branch rules ────────────────────────────────────────
        var branches = new Dictionary<string, (string AllowedCategories, string AllowedPartyTypes)>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(branchRulesJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(branchRulesJson);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var loc2 = element.GetProperty("location").GetString();
                    var cats = element.GetProperty("allowedCategories").GetString();
                    var types = element.GetProperty("allowedPartyTypes").GetString();
                    if (!string.IsNullOrEmpty(loc2))
                        branches[loc2] = (cats ?? string.Empty, types ?? string.Empty);
                }
            }
            catch { }
        }
        if (branches.Count == 0)
        {
            // Load only branches — the table is small, safe to load all
            var dbBranches = await db.Branches.IgnoreQueryFilters()
                .Select(x => new { x.Code, x.AllowedCategories, x.AllowedPartyTypes })
                .ToListAsync(cancellationToken);
            foreach (var br in dbBranches)
                branches[br.Code] = (br.AllowedCategories ?? string.Empty, br.AllowedPartyTypes ?? string.Empty);
        }



        // Column indexes lookup
        columns.TryGetValue("Location", out int colLoc);
        columns.TryGetValue("Part Category Code", out int colCat); var hasCatCode = columns.ContainsKey("Part Category Code");
        columns.TryGetValue("Party Type", out int colType); var hasPartyType = columns.ContainsKey("Party Type");
        columns.TryGetValue("Dealer Sub-Type", out int colSubType); var hasDealerSubType = columns.ContainsKey("Dealer Sub-Type");
        columns.TryGetValue("Dealer Code", out int colDealerCode); var hasDealerCode = columns.ContainsKey("Dealer Code");
        columns.TryGetValue("Consignee", out int colConsignee); var hasConsignee = columns.ContainsKey("Consignee");
        columns.TryGetValue("Fiscal Year", out int colFY); var hasFiscalYear = columns.ContainsKey("Fiscal Year");
        columns.TryGetValue("Quarter", out int colQuarter); var hasQuarter = columns.ContainsKey("Quarter");
        columns.TryGetValue("Document Num", out int colDocNum); var hasDocumentNum = columns.ContainsKey("Document Num");
        columns.TryGetValue("Remarks", out int colRemarks); var hasRemarks = columns.ContainsKey("Remarks");
        columns.TryGetValue("Net Retail DDL", out int colDdl); var hasNetRetailDdl = columns.ContainsKey("Net Retail DDL");
        columns.TryGetValue("Day", out int colDay); var hasDay = columns.ContainsKey("Day");
        columns.TryGetValue("Part Num", out int colPartNum); var hasPartNum = columns.ContainsKey("Part Num");
        columns.TryGetValue("Root Part Num", out int colRootPartNum); var hasRootPartNum = columns.ContainsKey("Root Part Num");
        columns.TryGetValue("Net Retail Qty", out int colNetRetailQty); var hasNetRetailQty = columns.ContainsKey("Net Retail Qty");
        columns.TryGetValue("Month Year", out int colMonthYear); var hasMonthYear = columns.ContainsKey("Month Year");
        columns.TryGetValue("Month", out int colMonth); var hasMonth = columns.ContainsKey("Month");
        var hasYear = columns.ContainsKey("Year") || columns.ContainsKey("Fiscal Year");
        columns.TryGetValue("Year", out int colYear);
        columns.TryGetValue("Cons Party Code", out int colPartyCode);
        columns.TryGetValue("Cons Party Name", out int colPartyName);
        columns.TryGetValue("Net Retail Selling", out int colSale);
        columns.TryGetValue("Discount Amount", out int colDiscount); var hasDiscount = columns.ContainsKey("Discount Amount");
        columns.TryGetValue("Slab", out int colSlab);
        columns.TryGetValue("Incentive", out int colIncentive);
        int colAchievement = -1;
        if (columns.ContainsKey("Achievement Percent")) colAchievement = columns["Achievement Percent"];

        columns.TryGetValue("Transfer Amount", out int colTransAmt); var hasTransAmt = columns.ContainsKey("Transfer Amount");
        columns.TryGetValue("Payment Status", out int colPayStatus); var hasPayStatus = columns.ContainsKey("Payment Status");
        columns.TryGetValue("UTR", out int colUtr); var hasUtr = columns.ContainsKey("UTR");
        columns.TryGetValue("Payment Date", out int colPayDate); var hasPayDate = columns.ContainsKey("Payment Date");
        columns.TryGetValue("Beneficiary Name", out int colBeneficiary); var hasBeneficiary = columns.ContainsKey("Beneficiary Name");
        columns.TryGetValue("Bank Account Number", out int colBankAcc); var hasBankAcc = columns.ContainsKey("Bank Account Number");
        columns.TryGetValue("IFSC", out int colIfsc); var hasIfsc = columns.ContainsKey("IFSC");

        var resolvedMode = (!string.IsNullOrEmpty(uploadMode) && uploadMode != "Sales") ? uploadMode : (hasSlab && hasIncentive ? "PreCalculated" : "Dynamic");

        var rawRowsList = new List<ExcelRawRow>();

        void ProcessReaderRow(IExcelDataReader rdr, int index)
        {
            var loc = ReaderStr(rdr, colLoc);
            var cat = hasCatCode ? ReaderStr(rdr, colCat) : string.Empty;
            var type = hasPartyType ? ReaderStr(rdr, colType) : string.Empty;

            var rowMonth = period.Month;
            var rowYear = period.Year;

            if (hasMonthYear)
            {
                var myText = ReaderStr(rdr, colMonthYear);
                if (TryParsePeriod(myText, out var rowPeriod))
                {
                    rowMonth = rowPeriod.Month;
                    rowYear = rowPeriod.Year;
                }
            }
            else if (hasMonth)
            {
                var mText = ReaderStr(rdr, colMonth);
                int mVal = 0;
                if (int.TryParse(mText, out int mInt) && mInt >= 1 && mInt <= 12)
                    mVal = mInt;
                else
                {
                    var match = Regex.Match(mText, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)", RegexOptions.IgnoreCase);
                    if (match.Success)
                        mVal = DateTime.ParseExact(match.Value, "MMM", CultureInfo.InvariantCulture).Month;
                }

                int yVal = period.Year;
                if (hasYear)
                {
                    string yText = columns.ContainsKey("Year") ? ReaderStr(rdr, colYear) : ReaderStr(rdr, colFY);
                    var yearMatch = Regex.Match(yText, @"\d{2,4}");
                    if (yearMatch.Success)
                    {
                        yVal = int.Parse(yearMatch.Value, CultureInfo.InvariantCulture);
                        if (yVal < 100) yVal += 2000;
                    }
                }

                if (mVal >= 1 && mVal <= 12)
                {
                    rowMonth = mVal;
                    rowYear = yVal;
                }
            }

            string? subType = hasDealerSubType ? ReaderStr(rdr, colSubType) : null;
            if (string.IsNullOrEmpty(subType)) subType = "AW";

            string? dealerCode = hasDealerCode ? ReaderStr(rdr, colDealerCode) : null;
            string? consignee = hasConsignee ? ReaderStr(rdr, colConsignee) : null;
            
            string? fiscalYear = hasFiscalYear ? ReaderStr(rdr, colFY) : null;
            if (string.IsNullOrEmpty(fiscalYear))
            {
                fiscalYear = rowMonth >= 4 ? $"{rowYear}-{rowYear + 1}" : $"{rowYear - 1}-{rowYear}";
            }

            string? quarter = hasQuarter ? ReaderStr(rdr, colQuarter) : null;
            if (string.IsNullOrEmpty(quarter))
            {
                quarter = rowMonth switch
                {
                    4 or 5 or 6 => "Q1",
                    7 or 8 or 9 => "Q2",
                    10 or 11 or 12 => "Q3",
                    1 or 2 or 3 => "Q4",
                    _ => "Q1"
                };
            }

            string? docNum = hasDocumentNum ? ReaderStr(rdr, colDocNum) : null;
            if (string.IsNullOrEmpty(docNum) || docNum == "-" || docNum.Equals("N/A", StringComparison.OrdinalIgnoreCase))
            {
                docNum = $"UPL-{rowMonth:D2}-{index}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            }

            string? remarks = hasRemarks ? ReaderStr(rdr, colRemarks) : null;
            if (string.IsNullOrEmpty(remarks))
            {
                remarks = "Uploaded via Sales Upload";
            }

            decimal? ddlVal = hasNetRetailDdl ? (decimal?)ReaderDec(rdr, colDdl) : null;
            if (ddlVal == null)
            {
                var saleVal = ReaderDec(rdr, colSale);
                var disc = hasDiscount ? ReaderDec(rdr, colDiscount) : 0m;
                ddlVal = Math.Max(0, saleVal - disc);
            }

            int? dayVal = null;
            if (hasDay)
            {
                var dayText = ReaderStr(rdr, colDay);
                if (int.TryParse(dayText, out int parsedDay)) dayVal = parsedDay;
            }

            var partyCode = ReaderStr(rdr, colPartyCode);
            var partyName = ReaderStr(rdr, colPartyName);
            var originalPartyCode = partyCode;

            if (!string.IsNullOrEmpty(partyCode) && partyCode.Equals("10912NYI", StringComparison.OrdinalIgnoreCase))
            {
                type = "MASS";
            }

            decimal? transferAmtVal = hasTransAmt && !string.IsNullOrWhiteSpace(ReaderStr(rdr, colTransAmt)) ? (decimal?)ReaderDec(rdr, colTransAmt) : null;
            string? payStatusVal = hasPayStatus ? ReaderStr(rdr, colPayStatus) : null;
            if (string.IsNullOrWhiteSpace(payStatusVal)) payStatusVal = null;
            string? utrVal = hasUtr ? ReaderStr(rdr, colUtr) : null;
            if (string.IsNullOrWhiteSpace(utrVal)) utrVal = null;
            DateTime? payDateVal = null;
            if (hasPayDate)
            {
                var payDateText = ReaderStr(rdr, colPayDate);
                if (!string.IsNullOrWhiteSpace(payDateText))
                {
                    if (DateTime.TryParse(payDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                        payDateVal = d1;
                    else if (DateTime.TryParse(payDateText, new CultureInfo("en-GB"), DateTimeStyles.None, out var d2))
                        payDateVal = d2;
                    else if (double.TryParse(payDateText, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDouble))
                    {
                        try { payDateVal = DateTime.FromOADate(oaDouble); } catch {}
                    }
                }
            }
            string? beneficiaryVal = hasBeneficiary ? ReaderStr(rdr, colBeneficiary) : null;
            if (string.IsNullOrWhiteSpace(beneficiaryVal)) beneficiaryVal = null;
            string? bankAccVal = hasBankAcc ? ReaderStr(rdr, colBankAcc) : null;
            if (string.IsNullOrWhiteSpace(bankAccVal)) bankAccVal = null;
            string? ifscVal = hasIfsc ? ReaderStr(rdr, colIfsc) : null;
            if (string.IsNullOrWhiteSpace(ifscVal)) ifscVal = null;

            string? partNumVal = hasPartNum ? ReaderStr(rdr, colPartNum) : null;
            string? rootPartNumVal = hasRootPartNum ? ReaderStr(rdr, colRootPartNum) : null;
            int? netRetailQtyVal = null;
            if (hasNetRetailQty)
            {
                var qtyText = ReaderStr(rdr, colNetRetailQty);
                if (int.TryParse(qtyText, out int parsedQty)) netRetailQtyVal = parsedQty;
            }

            rawRowsList.Add(new ExcelRawRow
            {
                RowNumber = index,
                Month = rowMonth,
                Year = rowYear,
                PartyCode = partyCode,
                PartyName = partyName,
                OriginalPartyCode = originalPartyCode,
                Location = loc,
                PartyType = type,
                PartCategoryCode = cat,
                SaleValue = ReaderDec(rdr, colSale),
                Discount = hasDiscount ? ReaderDec(rdr, colDiscount) : 0m,
                SlabPercent = hasSlab ? CellPctFromReader(rdr, colSlab) : 0m,
                FileIncentive = hasIncentive ? Math.Round(ReaderDec(rdr, colIncentive), 0, MidpointRounding.AwayFromZero) : 0m,
                AchievementPercent = hasAchievement && colAchievement >= 0 ? ReaderDec(rdr, colAchievement) : 0m,
                DealerSubType = subType,
                Consignee = consignee,
                DealerCode = dealerCode,
                FiscalYear = fiscalYear,
                Quarter = quarter,
                DocumentNum = docNum,
                Remarks = remarks,
                NetRetailDdl = ddlVal,
                Day = dayVal,
                TransferAmount = transferAmtVal,
                PaymentStatus = payStatusVal,
                Utr = utrVal,
                PaymentDate = payDateVal,
                BeneficiaryName = beneficiaryVal,
                BankAccountNumber = bankAccVal,
                IFSC = ifscVal,
                PartNum = partNumVal,
                RootPartNum = rootPartNumVal,
                NetRetailQty = netRetailQtyVal
            });
        }

        // Process the first row (the one read to resolve period)
        int rowNumIndex = 2;
        ProcessReaderRow(reader, rowNumIndex);

        // Stream and process the remaining rows
        while (reader.Read())
        {
            if (limit.HasValue && rowNumIndex >= limit.Value)
            {
                break;
            }
            rowNumIndex++;
            ProcessReaderRow(reader, rowNumIndex);
        }

        var rawRows = rawRowsList;

        if (resolvedMode == "Dynamic")
        {
            var targetYears = rawRows.Select(x => x.Year).Distinct().ToList();
            var targetMonths = rawRows.Select(x => x.Month).Distinct().ToList();
            
            var hasExistingSales = await db.SsIncentives
                .IgnoreQueryFilters()
                .AnyAsync(x => targetYears.Contains(x.Year) && targetMonths.Contains(x.Month), cancellationToken);

            var dynamicRows = new List<SalesImportRow>(rawRows.Count);
            foreach (var row in rawRows)
            {
                dynamicRows.Add(new SalesImportRow(
                    row.RowNumber,
                    row.Month,
                    row.Year,
                    row.PartyCode,
                    row.PartyName,
                    row.Location,
                    row.SaleValue,
                    row.Discount,
                    0m,
                    0m,
                    0m,
                    row.AchievementPercent,
                    resolvedMode,
                    null,
                    row.OriginalPartyCode,
                    row.PartyType,
                    row.PartCategoryCode,
                    null,
                    row.DealerSubType,
                    row.Consignee,
                    row.FiscalYear,
                    row.Quarter,
                    row.DocumentNum,
                    row.Remarks,
                    row.NetRetailDdl,
                    row.Day,
                    row.DealerCode,
                    row.TransferAmount,
                    row.PaymentStatus,
                    row.Utr,
                    row.PaymentDate,
                    row.BeneficiaryName,
                    row.BankAccountNumber,
                    row.IFSC,
                    hasExistingSales ? "Duplicate" : "Valid",
                    row.OriginalPartyCode,
                    row.PartNum,
                    row.RootPartNum,
                    row.NetRetailQty
                ));
            }
            return dynamicRows;
        }

        var partyCodesInSheet = rawRows.Select(x => x.PartyCode).Distinct(StringComparer.OrdinalIgnoreCase)
            .Concat(rawRows.Select(x => x.OriginalPartyCode).Distinct(StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Load ONLY parties whose codes appear in the sheet — avoids full table scan on large party tables
        var partyCodesInSheetSet2 = new HashSet<string>(partyCodesInSheet, StringComparer.OrdinalIgnoreCase);
        var allParties = await db.Parties
            .IgnoreQueryFilters()
            .Where(x => partyCodesInSheetSet2.Contains(x.PartyCode))
            .ToListAsync(cancellationToken);

        var partiesInSheet = new Dictionary<string, Party>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in allParties)
        {
            if (!string.IsNullOrEmpty(p.PartyCode))
            {
                partiesInSheet[p.PartyCode] = p;
            }
        }

        var monthsInSheet = rawRows.Select(x => x.Month).Distinct().ToList();
        var yearsInSheet = rawRows.Select(x => x.Year).Distinct().ToList();

        // Fetch all sales incentives for target period to avoid Contains filter with thousands of parameters
        var rawExistingSales = await db.SsIncentives
            .Where(x => yearsInSheet.Contains(x.Year) && (isHistorical || monthsInSheet.Contains(x.Month)))
            .Select(x => new { x.Year, x.Month, x.PartyCode, x.Status, x.PaymentStatus })
            .ToListAsync(cancellationToken);

        var partyCodesInSheetSet = new HashSet<string>(partyCodesInSheet, StringComparer.OrdinalIgnoreCase);
        var existingSalesInSheet = rawExistingSales
            .Where(x => partyCodesInSheetSet.Contains(x.PartyCode))
            .ToList();

        var existingSalesLookup = existingSalesInSheet
            .GroupBy(x => $"{x.Year}-{x.Month}-{x.PartyCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Any(x => x.Status == "Approved" || x.Status == "Posted"),
                StringComparer.OrdinalIgnoreCase);

        var existingLedgersLookup = existingSalesInSheet
            .GroupBy(x => $"{x.Year}-{x.Month}-{x.PartyCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Any(x => x.PaymentStatus == "Paid" || x.PaymentStatus == "Approved"),
                StringComparer.OrdinalIgnoreCase);

        var originalCodeSales = rawRows
            .GroupBy(x => $"{x.Year}-{x.Month}-{x.OriginalPartyCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.SaleValue),
                StringComparer.OrdinalIgnoreCase);

        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<SalesImportRow>();
        foreach (var row in rawRows)
        {
            partiesInSheet.TryGetValue(row.PartyCode, out var party);
            partiesInSheet.TryGetValue(row.OriginalPartyCode, out var originalParty);
            var mode = resolvedMode;
            var slabPercent = row.SlabPercent;
            var fileIncentive = row.FileIncentive;
            var calculatedIncentive = 0m;

            var consolidatedSales = originalCodeSales.GetValueOrDefault($"{row.Year}-{row.Month}-{row.OriginalPartyCode}", row.SaleValue);
            var isFixedIncentive = (party != null && party.DealerType == "Fixed Incentive") || (originalParty != null && originalParty.DealerType == "Fixed Incentive");
            var fixedIncentivePercent = originalParty != null && originalParty.DealerType == "Fixed Incentive" ? originalParty.FixedIncentivePercent : (party != null ? party.FixedIncentivePercent : 0m);

            bool rowHasSlab = mode != "Dynamic";
            if (mode == "Dynamic")
            {
                slabPercent = 0m;
                calculatedIncentive = 0m;
                fileIncentive = 0m;
                rowHasSlab = false;
            }
            else if (isFixedIncentive)
            {
                slabPercent = fixedIncentivePercent / 100m;
                calculatedIncentive = Math.Round(Math.Max(0, row.SaleValue * slabPercent), 0, MidpointRounding.AwayFromZero);
                fileIncentive = calculatedIncentive;
            }
            else
            {
                calculatedIncentive = fileIncentive;
            }

            var validation = validationService.ValidateRow(
                row.RowNumber,
                row.PartyCode,
                row.PartyName,
                row.OriginalPartyCode,
                row.Location,
                row.PartCategoryCode,
                row.PartyType,
                row.Month,
                row.Year,
                row.SaleValue,
                row.DocumentNum,       // distinguish multiple invoices for same dealer/month
                mode == "Dynamic",
                scheme != null,
                rowHasSlab,
                branches,
                partiesInSheet,
                existingSalesLookup,
                existingLedgersLookup,
                processedKeys
            );

            var error = validation.ErrorMessage;
            var validationStatus = validation.ValidationStatus;

            rows.Add(new SalesImportRow(
                row.RowNumber,
                row.Month,
                row.Year,
                row.PartyCode,
                row.PartyName,
                row.Location,
                row.SaleValue,
                row.Discount,
                slabPercent,
                fileIncentive,
                calculatedIncentive,
                row.AchievementPercent,
                resolvedMode,
                error,
                row.OriginalPartyCode,
                row.PartyType,
                row.PartCategoryCode,
                null,
                row.DealerSubType,
                row.Consignee,
                row.FiscalYear,
                row.Quarter,
                row.DocumentNum,
                row.Remarks,
                row.NetRetailDdl,
                row.Day,
                row.DealerCode,
                row.TransferAmount,
                row.PaymentStatus,
                row.Utr,
                row.PaymentDate,
                row.BeneficiaryName,
                row.BankAccountNumber,
                row.IFSC,
                validationStatus,
                row.OriginalPartyCode,
                row.PartNum,
                row.RootPartNum,
                row.NetRetailQty
            ));
        }

        return rows;
    }

    private static (int periodMonth, int periodYear) ResolvePeriod(System.Data.DataSet ds, string fileName)
    {
        if (TryParsePeriod(fileName, out var period))
            return period;

        try
        {
            var raw = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(x => x.TableName.Equals("Raw", StringComparison.OrdinalIgnoreCase));
            if (raw != null && raw.Rows.Count > 0 && raw.Columns.Count >= 3)
            {
                var rawPeriod = raw.Rows[0][2]?.ToString();
                if (TryParsePeriod(rawPeriod, out period))
                    return period;
            }

            var ws = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(x => x.TableName.Equals("Summary", StringComparison.OrdinalIgnoreCase))
                ?? ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault();

            if (ws != null && ws.Rows.Count > 0)
            {
                int monthYearCol = -1;
                int monthCol = -1;
                int yearCol = -1;
                
                for (int col = 0; col < ws.Columns.Count; col++)
                {
                    var hText = ws.Columns[col].ColumnName.Trim();
                    if (hText.Equals("Month Year", StringComparison.OrdinalIgnoreCase))
                        monthYearCol = col;
                    else if (hText.Equals("Month", StringComparison.OrdinalIgnoreCase))
                        monthCol = col;
                    else if (hText.Equals("Year", StringComparison.OrdinalIgnoreCase) || hText.Equals("Fiscal Year", StringComparison.OrdinalIgnoreCase))
                        yearCol = col;
                }
                
                if (monthYearCol != -1)
                {
                    var monthCellText = ws.Rows[0][monthYearCol]?.ToString();
                    if (TryParsePeriod(monthCellText, out period))
                        return period;
                }
                
                if (monthCol != -1 && yearCol != -1)
                {
                    var mText = ws.Rows[0][monthCol]?.ToString();
                    var yText = ws.Rows[0][yearCol]?.ToString();
                    
                    int month = 0;
                    if (int.TryParse(mText, out int mInt) && mInt >= 1 && mInt <= 12)
                    {
                        month = mInt;
                    }
                    else if (!string.IsNullOrEmpty(mText))
                    {
                        var match = Regex.Match(mText, @"(Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            month = DateTime.ParseExact(match.Value, "MMM", CultureInfo.InvariantCulture).Month;
                        }
                    }
                    
                    int year = 0;
                    if (!string.IsNullOrEmpty(yText))
                    {
                        var yearMatch = Regex.Match(yText, @"\d{2,4}");
                        if (yearMatch.Success)
                        {
                            year = int.Parse(yearMatch.Value, CultureInfo.InvariantCulture);
                            if (year < 100) year += 2000;
                        }
                    }
                    
                    if (month >= 1 && month <= 12 && year > 0)
                    {
                        return (month, year);
                    }
                }

                if (ws.Columns.Count >= 4)
                {
                    var fallbackText = ws.Rows[0][3]?.ToString();
                    if (TryParsePeriod(fallbackText, out period))
                        return period;
                }
            }
        }
        catch {}

        return (DateTime.UtcNow.Month, DateTime.UtcNow.Year);
    }

    private static bool TryParsePeriod(string? text, out (int Month, int Year) period)
    {
        period = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Try standard DateTime parsing
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            period = (dt.Month, dt.Year);
            return true;
        }

        // Try specific cultures (en-GB, en-US)
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-GB"), DateTimeStyles.None, out dt))
        {
            period = (dt.Month, dt.Year);
            return true;
        }
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.None, out dt))
        {
            period = (dt.Month, dt.Year);
            return true;
        }

        // Try parsing Excel OADate serial number (between year 2000 and 2100)
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
        {
            if (d >= 36526 && d <= 73050) // Year 2000 to 2100
            {
                try
                {
                    var dateFromOa = DateTime.FromOADate(d);
                    period = (dateFromOa.Month, dateFromOa.Year);
                    return true;
                }
                catch {}
            }
        }

        // Regex fallback for month names (e.g. "Mar 2026", "Mar-26", etc.)
        var match = Regex.Match(text, @"(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*[\s'-]*(?<year>\d{2,4})", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var month = DateTime.ParseExact(match.Groups["month"].Value[..3], "MMM", CultureInfo.InvariantCulture).Month;
            var yearText = match.Groups["year"].Value;
            var year = int.Parse(yearText, CultureInfo.InvariantCulture);
            period = (month, year < 100 ? 2000 + year : year);
            return true;
        }

        return false;
    }

    private static string GetText(IXLCell cell)
    {
        var value = cell.CachedValue;
        return value.IsBlank ? string.Empty : value.ToString(CultureInfo.InvariantCulture).Trim();
    }

    private static decimal CalculateWorkbookIncentive(decimal saleValue, decimal discount, decimal slabPercent)
        => Math.Round(Math.Max(0, saleValue * slabPercent), 0, MidpointRounding.AwayFromZero);

    private static bool IsHeaderRecognized(string header, IReadOnlyList<ColumnMappingRule> dbMappings)
    {
        var raw = header.Trim();
        if (string.IsNullOrEmpty(raw)) return false;

        // 1. Check custom database mappings
        if (dbMappings.Any(m => raw.Equals(m.ExcelHeader, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 2. Check header aliases
        if (_headerAliases.ContainsKey(raw))
            return true;

        // 3. Check field to key mappings (keys and values)
        if (_fieldToKey.ContainsKey(raw))
            return true;

        if (_fieldToKey.Values.Any(v => raw.Equals(v, StringComparison.OrdinalIgnoreCase)))
            return true;

        // 4. Also check direct field names of RawRecord and other DTOs
        var rawRecordFields = new[] { 
            "DealerSubType", "Consignee", "DealerCode", "Loc", "PartCategoryCode", "FiscalYear", 
            "Quarter", "Month", "MonthYear", "ConsPartyCode", "ConsPartyName", "PartyType", 
            "DocumentNum", "Remarks", "NetRetailSelling", "DiscountAmount", "NetRetailDdl", 
            "OriginalCode", "Day", "TransferAmount", "PaymentStatus", "UTR", "PaymentDate", 
            "BeneficiaryName", "BankAccountNumber", "IFSC", "Slab", "Incentive", "AchievementPercent",
            "Cons Party Code", "Cons Party Name", "Month Year", "Location", "Net Retail Selling",
            "Discount Amount", "Achievement Percent", "Slab", "Incentive", "Part Category Code",
            "Party Type", "Original Code", "Net Retail DDL", "Dealer Sub-Type", "Dealer Code"
        };
        if (rawRecordFields.Contains(raw, StringComparer.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string NormalizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;
        return new string(header.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static void ValidateSalesHeaders(System.Data.DataTable dt)
    {
        var requiredNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "consignee",
            "dealercode",
            "loc",
            "partcategorycode",
            "fiscalyear",
            "month",
            "monthyear",
            "conspartycode",
            "conspartyname",
            "partytype",
            "netretailselling",
            "discountamount"
        };

        var optionalAndNewNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dealersubtype",
            "quarter",
            "documentnum",
            "remarks",
            "netretailddl",
            "partnum",
            "rootpartnum",
            "day",
            "netretailqty"
        };

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["consignee"] = "Consignee",
            ["dealercode"] = "Dealer Code",
            ["loc"] = "Loc",
            ["partcategorycode"] = "Part Category Code",
            ["fiscalyear"] = "Fiscal Year",
            ["month"] = "Month",
            ["monthyear"] = "Month Year",
            ["conspartycode"] = "Cons Party Code",
            ["conspartyname"] = "Cons Party Name",
            ["partytype"] = "Party Type",
            ["netretailselling"] = "Net Retail Selling",
            ["discountamount"] = "Discount Amount"
        };

        var sheetHeaders = new List<string>();
        var normalizedSheetHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unrecognizedHeaders = new List<string>();

        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var header = dt.Columns[c].ColumnName?.Trim();
            if (string.IsNullOrEmpty(header)) continue;

            var normalized = NormalizeHeader(header);
            
            // Map variations of Part Category Code to standard partcategorycode
            if (normalized == "partcategoryco" || normalized == "partcategorycoc" || normalized == "partcategorycod")
            {
                normalized = "partcategorycode";
            }

            if (!requiredNormalized.Contains(normalized) && !optionalAndNewNormalized.Contains(normalized))
            {
                unrecognizedHeaders.Add(header);
            }
            else
            {
                sheetHeaders.Add(header);
                normalizedSheetHeaders.Add(normalized);
            }
        }

        var missingHeaders = requiredNormalized
            .Where(expected => !normalizedSheetHeaders.Contains(expected))
            .Select(expected => displayNames[expected])
            .ToList();

        if (unrecognizedHeaders.Count > 0 || missingHeaders.Count > 0)
        {
            var errorParts = new List<string>();
            if (missingHeaders.Count > 0)
            {
                errorParts.Add($"Missing required column(s): {string.Join(", ", missingHeaders)}");
            }
            if (unrecognizedHeaders.Count > 0)
            {
                errorParts.Add($"Unrecognized/invalid column(s): {string.Join(", ", unrecognizedHeaders)}");
            }
            throw new InvalidOperationException($"Uploaded Excel sheet structure is invalid. {string.Join(". ", errorParts)}");
        }
    }

    private static void ValidatePreCalculatedHeaders(System.Data.DataTable dt)
    {
        // Exact 8-column format required for Pre-Calculated imports:
        // Month | Cons Party Code | Cons Party Name | Location |
        // Net Retail Selling | Discount Amount | Slab | Incentive
        var expectedNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "month",
            "conspartycode",
            "conspartyname",
            "location",
            "netretailselling",
            "discountamount",
            "slab",
            "incentive"
        };

        var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["month"]            = "Month",
            ["conspartycode"]    = "Cons Party Code",
            ["conspartyname"]    = "Cons Party Name",
            ["location"]         = "Location",
            ["netretailselling"] = "Net Retail Selling",
            ["discountamount"]   = "Discount Amount",
            ["slab"]             = "Slab",
            ["incentive"]        = "Incentive"
        };

        var normalizedSheetHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unrecognizedHeaders    = new List<string>();

        for (int c = 0; c < dt.Columns.Count; c++)
        {
            var header = dt.Columns[c].ColumnName?.Trim();
            if (string.IsNullOrEmpty(header)) continue;

            var normalized = NormalizeHeader(header);
            if (!expectedNormalized.Contains(normalized))
                unrecognizedHeaders.Add(header);
            else
                normalizedSheetHeaders.Add(normalized);
        }

        var missingHeaders = expectedNormalized
            .Where(expected => !normalizedSheetHeaders.Contains(expected))
            .Select(expected => displayNames[expected])
            .ToList();

        if (unrecognizedHeaders.Count > 0 || missingHeaders.Count > 0)
        {
            var errorParts = new List<string>();
            if (missingHeaders.Count > 0)
                errorParts.Add($"Missing required column(s): {string.Join(", ", missingHeaders)}");
            if (unrecognizedHeaders.Count > 0)
                errorParts.Add($"Unrecognized/invalid column(s): {string.Join(", ", unrecognizedHeaders)}");
            throw new InvalidOperationException(
                $"Uploaded Excel sheet structure is invalid for Pre-Calculated mode. " +
                $"Expected exactly 8 columns: Month, Cons Party Code, Cons Party Name, Location, " +
                $"Net Retail Selling, Discount Amount, Slab, Incentive. " +
                $"{string.Join(". ", errorParts)}");
        }
    }

    private sealed class ExcelRawRow
    {
        public int RowNumber { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string PartyCode { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty;
        public string OriginalPartyCode { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string PartyType { get; set; } = string.Empty;
        public string PartCategoryCode { get; set; } = string.Empty;
        public decimal SaleValue { get; set; }
        public decimal Discount { get; set; }
        public decimal SlabPercent { get; set; }
        public decimal FileIncentive { get; set; }
        public decimal AchievementPercent { get; set; }
        public string? DealerSubType { get; set; }
        public string? Consignee { get; set; }
        public string? DealerCode { get; set; }
        public string? FiscalYear { get; set; }
        public string? Quarter { get; set; }
        public string? DocumentNum { get; set; }
        public string? Remarks { get; set; }
        public decimal? NetRetailDdl { get; set; }
        public int? Day { get; set; }
        public decimal? TransferAmount { get; set; }
        public string? PaymentStatus { get; set; }
        public string? Utr { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string? BeneficiaryName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? IFSC { get; set; }
        public string? PartNum { get; set; }
        public string? RootPartNum { get; set; }
        public int? NetRetailQty { get; set; }
    }
}
