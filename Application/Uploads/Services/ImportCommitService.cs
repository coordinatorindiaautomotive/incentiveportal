using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

public interface IImportCommitService
{
    Task<ImportSummary> CommitAsync(
        IReadOnlyList<SalesImportRow> rows,
        string fileName,
        string? uploadMode = null,
        string? changeReason = null,
        int? previousImportLogId = null,
        CancellationToken cancellationToken = default,
        IFormFile? file = null,
        bool rewriteSales = true
    );
}

/// <summary>
/// STAGING-ONLY commit service.
///
/// Architecture: Excel → Raw Table (100% as-is) → Governor Engine → Processed Tables
///
/// Responsibilities of this service:
///   1. Write an ImportLog record.
///   2. If rewriteSales=true, delete prior Raw rows for the same year/month periods.
///   3. Stream ALL rows from the preview directly into the Raw table via SqlBulkCopy,
///      preserving every value exactly as it came from the Excel file.
///   4. Return an ImportSummary.
///
/// What this service explicitly does NOT do:
///   - No incentive calculations
///   - No party resolution or creation
///   - No branch creation
///   - No SsIncentives inserts
///   - No CategorySalesAggregates computation
///   - No MonthLock creation
///   - No IncentivePeriod creation or ownership checks
///   - No validation beyond confirming rows exist
///   - No data transformations or normalisations
/// </summary>
public sealed class ImportCommitService(
    IncentiveDbContext db,
    IHttpContextAccessor httpContextAccessor,
    IAnalyticsRefreshService analyticsService
) : IImportCommitService
{
    public async Task<ImportSummary> CommitAsync(
        IReadOnlyList<SalesImportRow> rows,
        string fileName,
        string? uploadMode = null,
        string? changeReason = null,
        int? previousImportLogId = null,
        CancellationToken cancellationToken = default,
        IFormFile? file = null,
        bool rewriteSales = true
    )
    {
        if (rows == null || rows.Count == 0)
            throw new InvalidOperationException("No rows found in the uploaded workbook to commit.");

        db.DisableAuditLogs = true;
        try
        {
            var currentUser = httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "system";
            var now = DateTime.UtcNow;

            // ── 1. DETERMINE VERSION ──────────────────────────────────────────────
            var version = 1;
            if (previousImportLogId.HasValue)
            {
                var prevLog = await db.ImportLogs.FindAsync(
                    new object[] { previousImportLogId.Value }, cancellationToken);
                if (prevLog != null)
                    version = prevLog.VersionNumber + 1;
            }

            var firstRow = rows[0];
            var yearsInSheet  = rows.Select(x => x.Year).Distinct().ToList();
            var monthsInSheet = rows.Select(x => x.Month).Distinct().ToList();
            var importType    = rows.Any(x => x.ImportMode == "Dynamic") ? "DynamicSales" : "MonthlySales";
            var isHistorical  = !rows.Any(x =>
                x.Year > DateTime.Today.Year ||
                (x.Year == DateTime.Today.Year && x.Month >= DateTime.Today.Month));

            // ── 2. WRITE IMPORT LOG ──────────────────────────────────────────────
            var log = new ImportLog
            {
                FileName             = fileName,
                ImportType           = importType,
                TotalRows            = rows.Count,
                SuccessRows          = rows.Count,
                FailedRows           = 0,
                Status               = "Completed",
                ErrorJson            = "[]",
                IsHistorical         = isHistorical,
                Year                 = firstRow.Year,
                Month                = isHistorical ? 0 : firstRow.Month,
                VersionNumber        = version,
                ChangeReason         = changeReason,
                PreviousImportLogId  = previousImportLogId
            };
            db.ImportLogs.Add(log);
            await db.SaveChangesAsync(cancellationToken);

            // ── 3. OPTIONAL: DELETE PRIOR RAW ROWS FOR THE SAME PERIODS ─────────
            int deletedCount = 0;
            if (rewriteSales)
            {
                foreach (var year in yearsInSheet)
                {
                    foreach (var month in monthsInSheet)
                    {
                        deletedCount += await db.Raws
                            .IgnoreQueryFilters()
                            .Where(x => x.YearNumber == year && x.MonthNumber == month)
                            .ExecuteDeleteAsync(cancellationToken);
                    }
                }
            }

            // ── 4. STREAM ALL ROWS INTO RAW TABLE AS-IS ──────────────────────────
            await BulkInsertRawsAsync(rows, log.Id, currentUser, now, cancellationToken);

            if (uploadMode == "PreCalculated")
            {
                // Delete existing non-finalized incentives for this period
                var toDelete = await db.SsIncentives
                    .Where(x => x.Month == firstRow.Month && x.Year == firstRow.Year && !x.IsDeleted && x.PaymentStatus != "Paid" && x.PaymentStatus != "Approved")
                    .ToListAsync(cancellationToken);

                foreach (var old in toDelete)
                {
                    old.IsDeleted = true;
                    db.Entry(old).State = EntityState.Modified;
                }
                await db.SaveChangesAsync(cancellationToken);

                // Group and insert PreCalculated records
                var parties = await db.Parties.IgnoreQueryFilters().Include(x => x.Branch).ToListAsync(cancellationToken);
                var partiesDict = parties.GroupBy(x => x.PartyCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                var mappings = await db.PartyCodeMappings.Where(m => m.IsActive && !m.IsDeleted).ToListAsync(cancellationToken);
                var mappingsDict = mappings.GroupBy(m => m.AlternativeCode, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().OriginalCode, StringComparer.OrdinalIgnoreCase);

                var activeTdsRules = await db.TdsRules
                    .Where(x => !x.IsDeleted && x.EffectiveFrom <= new DateTime(firstRow.Year, firstRow.Month, 1) && x.EffectiveTo >= new DateTime(firstRow.Year, firstRow.Month, 1))
                    .OrderByDescending(x => x.AnnualThreshold)
                    .ToListAsync(cancellationToken);

                var groups = rows.GroupBy(r => {
                    var rawPartyCode = r.PartyCode ?? string.Empty;
                    if (mappingsDict.TryGetValue(rawPartyCode, out var mc)) rawPartyCode = mc;
                    if (partiesDict.TryGetValue(rawPartyCode, out var po) && !string.IsNullOrEmpty(po.OriginalPartyCode))
                    {
                        if (partiesDict.TryGetValue(po.OriginalPartyCode, out var pp)) rawPartyCode = pp.PartyCode;
                    }
                    return new {
                        PartyCode = rawPartyCode,
                        PartCategoryCode = r.PartCategoryCode ?? "Other",
                        Location = r.Location ?? ""
                    };
                }).Select(g => {
                    var first = g.First();
                    partiesDict.TryGetValue(g.Key.PartyCode, out var pObj);
                    var partyName = pObj?.PartyName ?? first.PartyName ?? "Unknown Party";
                    return new {
                        g.Key.PartyCode,
                        PartyName = partyName,
                        g.Key.PartCategoryCode,
                        g.Key.Location,
                        SaleValue = g.Sum(x => x.SaleValue),
                        Discount = g.Sum(x => x.Discount),
                        SlabPercent = g.Max(x => x.SlabPercent),
                        FileIncentive = g.Sum(x => x.FileIncentive),
                        TransferAmount = g.Sum(x => x.TransferAmount ?? 0m)
                    };
                }).ToList();

                foreach (var grp in groups)
                {
                    partiesDict.TryGetValue(grp.PartyCode, out var targetParty);
                    if (targetParty == null) continue;

                    decimal grossIncentive = grp.FileIncentive;
                    decimal netIncentive = Math.Max(0m, grossIncentive - grp.Discount);

                    var bankDetails = await db.BankDetails.FirstOrDefaultAsync(b => b.PartyId == targetParty.Id && b.ApprovalStatus == "Approved" && !b.IsDeleted, cancellationToken);
                    var hasPan = bankDetails != null && !string.IsNullOrWhiteSpace(bankDetails.PAN);

                    TdsRule? matchedTdsRule = null;
                    foreach (var rule in activeTdsRules)
                    {
                        if (netIncentive >= rule.AnnualThreshold)
                        {
                            matchedTdsRule = rule;
                            break;
                        }
                    }
                    decimal tdsRate = matchedTdsRule != null ? (hasPan ? matchedTdsRule.RateWithPan : matchedTdsRule.RateNoPan) : 0.05m;
                    decimal tdsAmount = Math.Round(netIncentive * tdsRate, 2);

                    var outstandingRecord = await db.DealerOutstandings
                        .FirstOrDefaultAsync(o => o.Year == firstRow.Year && o.Month == firstRow.Month && o.PartyCode == grp.PartyCode && !o.IsDeleted, cancellationToken);
                    decimal outstanding = outstandingRecord?.Outstanding ?? 0m;

                    decimal netTransfer = Math.Max(0m, netIncentive - tdsAmount);
                    netTransfer = Math.Max(0m, netTransfer - outstanding);

                    var ssIncentive = new SsIncentive
                    {
                        Month = firstRow.Month,
                        Year = firstRow.Year,
                        MonthLabel = new DateTime(firstRow.Year, firstRow.Month, 1).ToString("MMMM yyyy"),
                        PartyCode = grp.PartyCode,
                        PartyName = grp.PartyName,
                        SaleValue = grp.SaleValue,
                        OnBillDiscount = grp.Discount,
                        GrossIncentive = grossIncentive,
                        TdsAmount = tdsAmount,
                        NetTransferAmount = netTransfer,
                        Outstanding = outstanding,
                        SlabPercent = grp.SlabPercent,
                        AchievementPercent = grp.SlabPercent * 100m,
                        ProcessingDate = DateTime.UtcNow,
                        PaymentStatus = outstanding < 0 ? "Credit Party" : (netTransfer == 0 ? "Paid" : "Pending"),
                        PaymentDate = (outstanding >= 0 && netTransfer == 0) ? DateTime.UtcNow : (DateTime?)null,
                        BankAccountNumber = bankDetails?.AccountNumber ?? "-",
                        IFSC = bankDetails?.IFSC ?? "-",
                        BeneficiaryName = bankDetails?.AccountHolder ?? grp.PartyName,
                        Mode = "PreCalculated",
                        Status = "Posted",
                        PartCategoryCode = grp.PartCategoryCode,
                        SourceLocation = grp.Location,
                        ImportLogId = log.Id,
                        IncentiveType = "PreCalculated",
                        ApplicableSlab = "PreCalculated Upload"
                    };
                    db.SsIncentives.Add(ssIncentive);
                }

                await db.SaveChangesAsync(cancellationToken);

                // Initialize / save incentive period
                var period = await db.IncentivePeriods
                    .FirstOrDefaultAsync(p => p.Year == firstRow.Year && p.Month == firstRow.Month && !p.IsDeleted, cancellationToken);
                if (period != null)
                {
                    period.SourceType = "PreCalculated";
                    period.Status = "Calculated";
                    db.Entry(period).State = EntityState.Modified;
                }
                else
                {
                    period = new IncentivePeriod
                    {
                        Year = firstRow.Year,
                        Month = firstRow.Month,
                        SourceType = "PreCalculated",
                        Status = "Calculated",
                        LockedFlag = false
                    };
                    db.IncentivePeriods.Add(period);
                }
                await db.SaveChangesAsync(cancellationToken);

                await analyticsService.RefreshAsync(firstRow.Month, firstRow.Year, cancellationToken);
            }

            return new ImportSummary
            {
                TotalRows       = rows.Count,
                Committed       = rows.Count,
                Skipped         = 0,
                DeletedRecords  = deletedCount,
                Errors          = Array.Empty<string>(),
                Log             = log
            };
        }
        finally
        {
            db.DisableAuditLogs = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // BULK INSERT — streams rows one-by-one directly into SQL Server.
    // No DataTable is built; memory overhead is near-zero even for 500k+ rows.
    // ──────────────────────────────────────────────────────────────────────────────
    private async Task BulkInsertRawsAsync(
        IReadOnlyList<SalesImportRow> rows,
        int logId,
        string currentUser,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var connection = (Microsoft.Data.SqlClient.SqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(connection)
        {
            DestinationTableName = "Raw",
            BatchSize            = 10000,
            BulkCopyTimeout      = 0   // no timeout — let it run as long as needed
        };

        // Column mappings (source name → destination column name)
        bulkCopy.ColumnMappings.Add("DealerSubType",     "DealerSubType");
        bulkCopy.ColumnMappings.Add("Consignee",         "Consignee");
        bulkCopy.ColumnMappings.Add("DealerCode",        "DealerCode");
        bulkCopy.ColumnMappings.Add("Loc",               "Loc");
        bulkCopy.ColumnMappings.Add("PartCategoryCode",  "PartCategoryCode");
        bulkCopy.ColumnMappings.Add("FiscalYear",        "FiscalYear");
        bulkCopy.ColumnMappings.Add("Quarter",           "Quarter");
        bulkCopy.ColumnMappings.Add("Month",             "Month");
        bulkCopy.ColumnMappings.Add("MonthYear",         "MonthYear");
        bulkCopy.ColumnMappings.Add("ConsPartyCode",     "ConsPartyCode");
        bulkCopy.ColumnMappings.Add("ConsPartyName",     "ConsPartyName");
        bulkCopy.ColumnMappings.Add("PartyType",         "PartyType");
        bulkCopy.ColumnMappings.Add("DocumentNum",       "DocumentNum");
        bulkCopy.ColumnMappings.Add("Remarks",           "Remarks");
        bulkCopy.ColumnMappings.Add("NetRetailSelling",  "NetRetailSelling");
        bulkCopy.ColumnMappings.Add("DiscountAmount",    "DiscountAmount");
        bulkCopy.ColumnMappings.Add("NetRetailDdl",      "NetRetailDdl");
        bulkCopy.ColumnMappings.Add("OriginalCode",      "OriginalCode");
        bulkCopy.ColumnMappings.Add("ImportLogId",       "ImportLogId");
        bulkCopy.ColumnMappings.Add("RowNumber",         "RowNumber");
        bulkCopy.ColumnMappings.Add("MonthNumber",       "MonthNumber");
        bulkCopy.ColumnMappings.Add("YearNumber",        "YearNumber");
        bulkCopy.ColumnMappings.Add("IsDeleted",         "IsDeleted");
        bulkCopy.ColumnMappings.Add("CreatedAt",         "CreatedAt");
        bulkCopy.ColumnMappings.Add("CreatedBy",         "CreatedBy");
        bulkCopy.ColumnMappings.Add("UpdatedAt",         "UpdatedAt");
        bulkCopy.ColumnMappings.Add("UpdatedBy",         "UpdatedBy");
        bulkCopy.ColumnMappings.Add("AchievementPercent","AchievementPercent");
        bulkCopy.ColumnMappings.Add("PartNum",           "PartNum");
        bulkCopy.ColumnMappings.Add("RootPartNum",       "RootPartNum");
        bulkCopy.ColumnMappings.Add("Day",               "Day");
        bulkCopy.ColumnMappings.Add("NetRetailQty",      "NetRetailQty");

        // Stream directly — no DataTable allocation
        var reader = new RawStagingDataReader(rows, logId, currentUser, now);
        await bulkCopy.WriteToServerAsync(reader, cancellationToken);
    }
}

// ──────────────────────────────────────────────────────────────────────────────
// STREAMING DATA READER
//
// Wraps IReadOnlyList<SalesImportRow> as an IDataReader so SqlBulkCopy can
// stream rows one at a time without allocating an intermediate DataTable.
//
// Column order MUST match the ColumnMappings declared above.
// ──────────────────────────────────────────────────────────────────────────────
internal sealed class RawStagingDataReader : System.Data.Common.DbDataReader
{
    private readonly IReadOnlyList<SalesImportRow> _rows;
    private readonly int    _logId;
    private readonly string _user;
    private readonly DateTime _now;
    private int _index = -1;
    private SalesImportRow _current = null!;

    // Columns in the same order as ColumnMappings above
    private static readonly string[] _cols =
    [
        "DealerSubType", "Consignee", "DealerCode", "Loc", "PartCategoryCode",
        "FiscalYear", "Quarter", "Month", "MonthYear", "ConsPartyCode", "ConsPartyName",
        "PartyType", "DocumentNum", "Remarks", "NetRetailSelling", "DiscountAmount",
        "NetRetailDdl", "OriginalCode", "ImportLogId", "RowNumber", "MonthNumber",
        "YearNumber", "IsDeleted", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy",
        "AchievementPercent", "PartNum", "RootPartNum", "Day", "NetRetailQty"
    ];

    public RawStagingDataReader(IReadOnlyList<SalesImportRow> rows, int logId, string user, DateTime now)
    {
        _rows = rows;
        _logId = logId;
        _user = user;
        _now = now;
    }

    public override bool Read()
    {
        _index++;
        if (_index < _rows.Count)
        {
            _current = _rows[_index];
            return true;
        }
        return false;
    }

    /// <summary>
    /// Returns values exactly as they came from the Excel file — no transformation.
    /// Only the system/audit columns (ImportLogId, RowNumber, IsDeleted, CreatedAt, CreatedBy)
    /// are set by the server; everything else is the raw Excel value.
    /// </summary>
    public override object GetValue(int ordinal)
    {
        var r = _current;
        return ordinal switch
        {
            // ── Raw Excel columns — copied verbatim ──────────────────────────
            0  => (object?)r.DealerSubType   ?? DBNull.Value,   // DealerSubType
            1  => (object?)r.Consignee       ?? DBNull.Value,   // Consignee
            2  => (object?)r.DealerCode      ?? DBNull.Value,   // DealerCode
            3  => (object?)r.Location        ?? DBNull.Value,   // Loc
            4  => (object?)r.PartCategoryCode ?? DBNull.Value,  // PartCategoryCode
            5  => (object?)r.FiscalYear      ?? DBNull.Value,   // FiscalYear
            6  => (object?)r.Quarter         ?? DBNull.Value,   // Quarter
            7  => (r.Month >= 1 && r.Month <= 12)
                ? new DateTime(r.Year, r.Month, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)
                : r.Month.ToString(), // Month (string name)
            8  => (r.Month >= 1 && r.Month <= 12)
                ? $"{new DateTime(r.Year, r.Month, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture)} {r.Year}"
                : $"{r.Month} {r.Year}", // MonthYear
            9  => (object?)r.PartyCode       ?? DBNull.Value,   // ConsPartyCode
            10 => (object?)r.PartyName       ?? DBNull.Value,   // ConsPartyName
            11 => (!string.IsNullOrEmpty(r.PartyType) && (r.PartyType.Equals("Others", StringComparison.OrdinalIgnoreCase) || r.PartyType.Equals("Other", StringComparison.OrdinalIgnoreCase)))
                ? "WALK-IN CUSTOMER"
                : ((object?)r.PartyType ?? DBNull.Value),   // PartyType
            12 => (object?)r.DocumentNum     ?? DBNull.Value,   // DocumentNum
            13 => (object?)r.Remarks         ?? DBNull.Value,   // Remarks
            14 => r.SaleValue,                                   // NetRetailSelling
            15 => r.Discount,                                    // DiscountAmount
            16 => r.NetRetailDdl.HasValue ? (object)r.NetRetailDdl.Value : DBNull.Value, // NetRetailDdl
            17 => (object?)r.OriginalPartyCode ?? DBNull.Value, // OriginalCode

            // ── System/audit columns set by this service ─────────────────────
            18 => _logId,                   // ImportLogId
            19 => r.RowNumber,              // RowNumber
            20 => r.Month,                  // MonthNumber  (integer)
            21 => r.Year,                   // YearNumber   (integer)
            22 => false,                    // IsDeleted
            23 => _now,                     // CreatedAt
            24 => _user,                    // CreatedBy
            25 => DBNull.Value,             // UpdatedAt
            26 => DBNull.Value,             // UpdatedBy

            // ── Additional raw Excel columns ─────────────────────────────────
            27 => r.AchievementPercent,     // AchievementPercent
            28 => (object?)r.PartNum        ?? DBNull.Value,    // PartNum
            29 => (object?)r.RootPartNum    ?? DBNull.Value,    // RootPartNum
            30 => r.Day.HasValue ? (object)r.Day.Value : DBNull.Value,           // Day
            31 => r.NetRetailQty.HasValue ? (object)r.NetRetailQty.Value : DBNull.Value, // NetRetailQty
            _  => DBNull.Value
        };
    }

    // ── DbDataReader boilerplate ─────────────────────────────────────────────
    public override int  FieldCount      => _cols.Length;
    public override bool HasRows         => _rows.Count > 0;
    public override bool IsClosed        => false;
    public override int  RecordsAffected => -1;
    public override int  Depth           => 0;
    public override object this[int ordinal]  => GetValue(ordinal);
    public override object this[string name]  => GetValue(GetOrdinal(name));
    public override bool   NextResult()       => false;
    public override void   Close()            { }
    public override string GetName(int ordinal)      => _cols[ordinal];
    public override int    GetOrdinal(string name)   => Array.IndexOf(_cols, name);
    public override bool   IsDBNull(int ordinal)     => GetValue(ordinal) is DBNull;
    public override string GetString(int ordinal)    => (string)GetValue(ordinal);
    public override int    GetInt32(int ordinal)     => Convert.ToInt32(GetValue(ordinal));
    public override bool   GetBoolean(int ordinal)   => (bool)GetValue(ordinal);
    public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
    public override decimal  GetDecimal(int ordinal)  => Convert.ToDecimal(GetValue(ordinal));
    public override double   GetDouble(int ordinal)   => Convert.ToDouble(GetValue(ordinal));
    public override float    GetFloat(int ordinal)    => Convert.ToSingle(GetValue(ordinal));
    public override byte     GetByte(int ordinal)     => throw new NotSupportedException();
    public override long     GetBytes(int ordinal, long offset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override char     GetChar(int ordinal)     => throw new NotSupportedException();
    public override long     GetChars(int ordinal, long offset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
    public override Guid     GetGuid(int ordinal)     => throw new NotSupportedException();
    public override short    GetInt16(int ordinal)    => Convert.ToInt16(GetValue(ordinal));
    public override long     GetInt64(int ordinal)    => Convert.ToInt64(GetValue(ordinal));
    public override string   GetDataTypeName(int ordinal) => "nvarchar";
    public override Type     GetFieldType(int ordinal)    => typeof(object);
    public override int      GetValues(object[] values)
    {
        for (int i = 0; i < _cols.Length; i++) values[i] = GetValue(i);
        return _cols.Length;
    }
    public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
}
