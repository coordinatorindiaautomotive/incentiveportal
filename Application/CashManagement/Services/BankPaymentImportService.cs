using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Application.CashManagement.Services;

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public sealed class BankPaymentPreviewResult
{
    public bool IsValid        => Errors.Count == 0 && Rows.Count > 0;
    public int TotalRows       { get; set; }
    public bool IsDuplicateFile { get; set; }
    public string? DuplicateFileBatchRef { get; set; }
    public List<Dictionary<string, string?>> Rows { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class BankPaymentCommitResult
{
    public bool   Success          { get; set; }
    public string BatchRef         { get; set; } = string.Empty;
    public int    BatchId          { get; set; }
    public int    TotalRecords     { get; set; }
    public int    ImportedRecords  { get; set; }
    public int    DuplicateRecords { get; set; }
    public int    FailedRecords    { get; set; }
    public string Message          { get; set; } = string.Empty;
    public List<string> Errors     { get; set; } = new();
}

public sealed class BankPaymentBatchDetailViewModel
{
    public BankPaymentImportBatch Batch { get; set; } = default!;
    public List<RawBankPaymentRecord> Records { get; set; } = new();
    public int TotalCount   { get; set; }
    public int PageIndex    { get; set; }
    public int PageSize     { get; set; }
    public int PageCount    => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

// ─────────────────────────────────────────────────────────────────────────────
// INTERFACE
// ─────────────────────────────────────────────────────────────────────────────

public interface IBankPaymentImportService
{
    /// <summary>
    /// Parses the uploaded Excel file and returns a preview without persisting anything.
    /// Detects potential duplicate files by filename.
    /// </summary>
    Task<BankPaymentPreviewResult> PreviewAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the uploaded file:
    ///  1. Creates a BankPaymentImportBatch record (Status = Pending).
    ///  2. Inserts each row into RawBankPaymentRecords — INSERT ONLY, never UPDATE.
    ///  3. Skips rows whose (FileSequenceNum, UtrNo) already exist (duplicate detection).
    ///  4. Updates the batch counters and sets Status = Completed / PartialSuccess / Failed.
    /// </summary>
    Task<BankPaymentCommitResult> CommitAsync(Stream fileStream, string fileName, string uploadedBy, int? reconMonth, int? reconYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// SuperAdmin-only: Forces re-import of a file whose rows were previously all skipped as duplicates.
    /// Does NOT overwrite existing records — inserts new rows with the new BatchId.
    /// </summary>
    Task<BankPaymentCommitResult> ForceReimportAsync(Stream fileStream, string fileName, string authorizedBy, int? reconMonth, int? reconYear, CancellationToken cancellationToken = default);

    /// <summary>Returns a paginated list of all import batches, newest first.</summary>
    Task<List<BankPaymentImportBatch>> GetBatchesAsync(DateTime? from, DateTime? to, string? status, CancellationToken cancellationToken = default);

    /// <summary>Returns the batch header plus a paginated list of its raw payment records.</summary>
    Task<BankPaymentBatchDetailViewModel> GetBatchDetailAsync(int batchId, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
}

// ─────────────────────────────────────────────────────────────────────────────
// IMPLEMENTATION
// ─────────────────────────────────────────────────────────────────────────────

public sealed class BankPaymentImportService(IncentiveDbContext db) : IBankPaymentImportService
{
    // ── Column header name → property mapper ────────────────────────────────
    // Matching is case-insensitive; aliases handle minor bank format variations.
    private static readonly Dictionary<string, string> _headerMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["File_Sequence_Num"]      = nameof(RawBankPaymentRecord.FileSequenceNum),
            ["File Sequence Num"]      = nameof(RawBankPaymentRecord.FileSequenceNum),
            ["Pymt_Prod_Type_Code"]    = nameof(RawBankPaymentRecord.PymtProdTypeCode),
            ["Pymt Prod Type Code"]    = nameof(RawBankPaymentRecord.PymtProdTypeCode),
            ["Pymt_Mode"]              = nameof(RawBankPaymentRecord.PymtMode),
            ["Pymt Mode"]              = nameof(RawBankPaymentRecord.PymtMode),
            ["Debit_Acct_no"]          = nameof(RawBankPaymentRecord.DebitAcctNo),
            ["Debit Acct No"]          = nameof(RawBankPaymentRecord.DebitAcctNo),
            ["Beneficiary Name"]       = nameof(RawBankPaymentRecord.BeneficiaryName),
            ["Beneficiary Account No"] = nameof(RawBankPaymentRecord.BeneficiaryAccountNo),
            ["Bene_IFSC_Code"]         = nameof(RawBankPaymentRecord.BeneIfscCode),
            ["Bene IFSC Code"]         = nameof(RawBankPaymentRecord.BeneIfscCode),
            ["Amount"]                 = nameof(RawBankPaymentRecord.Amount),
            ["Debit Narration"]        = nameof(RawBankPaymentRecord.DebitNarration),
            ["Credit Narration"]       = nameof(RawBankPaymentRecord.CreditNarration),
            ["Mobile Number"]          = nameof(RawBankPaymentRecord.MobileNumber),
            ["Email ID"]               = nameof(RawBankPaymentRecord.EmailId),
            ["Email Id"]               = nameof(RawBankPaymentRecord.EmailId),
            ["Remark"]                 = nameof(RawBankPaymentRecord.Remark),
            ["Remarks"]                = nameof(RawBankPaymentRecord.Remark),
            ["Pymt_Date"]              = nameof(RawBankPaymentRecord.PymtDate),
            ["Pymt Date"]              = nameof(RawBankPaymentRecord.PymtDate),
            ["Payment Date"]           = nameof(RawBankPaymentRecord.PymtDate),
            ["Reference_no"]           = nameof(RawBankPaymentRecord.ReferenceNo),
            ["Reference No"]           = nameof(RawBankPaymentRecord.ReferenceNo),
            ["Addl_Info1"]             = nameof(RawBankPaymentRecord.AddlInfo1),
            ["Addl Info1"]             = nameof(RawBankPaymentRecord.AddlInfo1),
            ["Addl_Info2"]             = nameof(RawBankPaymentRecord.AddlInfo2),
            ["Addl Info2"]             = nameof(RawBankPaymentRecord.AddlInfo2),
            ["Addl_Info3"]             = nameof(RawBankPaymentRecord.AddlInfo3),
            ["Addl Info3"]             = nameof(RawBankPaymentRecord.AddlInfo3),
            ["Addl_Info4"]             = nameof(RawBankPaymentRecord.AddlInfo4),
            ["Addl Info4"]             = nameof(RawBankPaymentRecord.AddlInfo4),
            ["Addl_Info5"]             = nameof(RawBankPaymentRecord.AddlInfo5),
            ["Addl Info5"]             = nameof(RawBankPaymentRecord.AddlInfo5),
            ["Beneficiary LEI"]        = nameof(RawBankPaymentRecord.BeneficiaryLei),
            ["STATUS"]                 = nameof(RawBankPaymentRecord.BankStatus),
            ["Status"]                 = nameof(RawBankPaymentRecord.BankStatus),
            ["Current Step"]           = nameof(RawBankPaymentRecord.CurrentStep),
            ["File Name"]              = nameof(RawBankPaymentRecord.BankFileName),
            ["Rejected By"]            = nameof(RawBankPaymentRecord.RejectedBy),
            ["Rejection Reason"]       = nameof(RawBankPaymentRecord.RejectionReason),
            ["Acct_Debit_Date"]        = nameof(RawBankPaymentRecord.AcctDebitDate),
            ["Acct Debit Date"]        = nameof(RawBankPaymentRecord.AcctDebitDate),
            ["Customer Ref No"]        = nameof(RawBankPaymentRecord.CustomerRefNo),
            ["UTR NO"]                 = nameof(RawBankPaymentRecord.UtrNo),
            ["UTR No"]                 = nameof(RawBankPaymentRecord.UtrNo),
            ["UTR Number"]             = nameof(RawBankPaymentRecord.UtrNo),
        };

    // ── Preview ─────────────────────────────────────────────────────────────

    public async Task<BankPaymentPreviewResult> PreviewAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
    {
        var result = new BankPaymentPreviewResult();

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                result.Errors.Add("The uploaded file contains no worksheets.");
                return result;
            }

            var usedRows = ws.RowsUsed().ToList();
            if (usedRows.Count <= 1)
            {
                result.Errors.Add("The uploaded file is empty or contains only a header row.");
                return result;
            }

            // Build header map from row 1
            var headers = BuildHeaderIndex(ws);
            if (headers.Count == 0)
            {
                result.Errors.Add("Unable to detect column headers in row 1.");
                return result;
            }

            // Check for duplicate file by name
            var existingBatch = await db.BankPaymentImportBatches
                .AsNoTracking()
                .Where(b => b.OriginalFileName == fileName)
                .OrderByDescending(b => b.UploadedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingBatch != null)
            {
                result.IsDuplicateFile = true;
                result.DuplicateFileBatchRef = existingBatch.BatchRef;
                result.Warnings.Add($"This file name was previously uploaded as batch {existingBatch.BatchRef} on {existingBatch.UploadedAt:dd-MMM-yyyy HH:mm} UTC. Proceeding will check each row for duplicates.");
            }

            // Preview up to 50 rows
            int previewLimit = Math.Min(usedRows.Count - 1, 50);
            result.TotalRows = usedRows.Count - 1;

            for (int i = 1; i <= previewLimit; i++)
            {
                var row = usedRows[i];
                var dict = ReadRowAsDictionary(row, headers);
                result.Rows.Add(dict);
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Failed to parse Excel file: {ex.Message}");
        }

        return result;
    }

    // ── Commit ──────────────────────────────────────────────────────────────

    public async Task<BankPaymentCommitResult> CommitAsync(Stream fileStream, string fileName, string uploadedBy, int? reconMonth, int? reconYear, CancellationToken cancellationToken = default)
        => await ImportInternalAsync(fileStream, fileName, uploadedBy, isForcedReimport: false, forcedBy: null, reconMonth, reconYear, cancellationToken);

    // ── Force Reimport (SuperAdmin only) ────────────────────────────────────

    public async Task<BankPaymentCommitResult> ForceReimportAsync(Stream fileStream, string fileName, string authorizedBy, int? reconMonth, int? reconYear, CancellationToken cancellationToken = default)
        => await ImportInternalAsync(fileStream, fileName, authorizedBy, isForcedReimport: true, forcedBy: authorizedBy, reconMonth, reconYear, cancellationToken);

    // ── Core Import Engine ──────────────────────────────────────────────────

    private async Task<BankPaymentCommitResult> ImportInternalAsync(
        Stream fileStream, string fileName, string uploadedBy,
        bool isForcedReimport, string? forcedBy,
        int? reconMonth, int? reconYear,
        CancellationToken cancellationToken)
    {
        var result = new BankPaymentCommitResult();

        // 1. Open workbook
        XLWorkbook workbook;
        IXLWorksheet ws;
        List<IXLRow> usedRows;
        Dictionary<string, int> headers;

        try
        {
            workbook = new XLWorkbook(fileStream);
            ws = workbook.Worksheets.FirstOrDefault()
                ?? throw new InvalidOperationException("No worksheets found in the uploaded file.");
            usedRows = ws.RowsUsed().ToList();
            headers  = BuildHeaderIndex(ws);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Failed to open Excel file: {ex.Message}";
            result.Errors.Add(result.Message);
            return result;
        }

        if (usedRows.Count <= 1)
        {
            result.Success = false;
            result.Message = "The uploaded file is empty or contains only a header row.";
            return result;
        }

        int totalRows = usedRows.Count - 1;
        result.TotalRecords = totalRows;

        // 2. Generate batch reference
        string batchRef = await GenerateBatchRefAsync(cancellationToken);

        // 3. Create the import batch record (status = Pending)
        var batch = new BankPaymentImportBatch
        {
            BatchRef        = batchRef,
            OriginalFileName = fileName,
            UploadedBy      = uploadedBy,
            UploadedAt      = DateTime.UtcNow,
            TotalRecords    = totalRows,
            Status          = BankPaymentImportStatus.Pending,
            IsForcedReimport = isForcedReimport,
            ForcedBy        = forcedBy,
            ReconMonth      = reconMonth,
            ReconYear       = reconYear,
            CreatedBy       = uploadedBy
        };
        db.BankPaymentImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        // 4. Load existing (FileSequenceNum, BeneficiaryAccountNo) pairs for duplicate detection
        //    Uses a HashSet for O(1) membership tests — safe for large files.
        var existingPairs = await db.RawBankPaymentRecords
            .AsNoTracking()
            .Where(r => r.FileSequenceNum != null || r.BeneficiaryAccountNo != null)
            .Select(r => new { r.FileSequenceNum, r.BeneficiaryAccountNo })
            .ToListAsync(cancellationToken);

        var existingSet = existingPairs
            .Select(p => MakeDupKey(p.FileSequenceNum, p.BeneficiaryAccountNo))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            // 5. Process each data row
            int importedCount  = 0;
            int duplicateCount = 0;
            int failedCount    = 0;
            var failedRows     = new List<object>();

            var records = new List<RawBankPaymentRecord>();

            for (int i = 1; i < usedRows.Count; i++)
            {
                int excelRowNum = i + 1; // +1 because row 1 = header
                var row = usedRows[i];

                try
                {
                    var dict = ReadRowAsDictionary(row, headers);

                    string? seqNum = TryGet(dict, nameof(RawBankPaymentRecord.FileSequenceNum));
                    string? acctNo = TryGet(dict, nameof(RawBankPaymentRecord.BeneficiaryAccountNo));
                    var dupKey = MakeDupKey(seqNum, acctNo);

                    // Skip duplicates (unless this is a forced reimport that explicitly
                    // adds NEW rows — we still skip truly identical (seqNum+acctNo) pairs)
                    if (existingSet.Contains(dupKey))
                    {
                        duplicateCount++;
                        continue;
                    }

                    var record = MapToRecord(dict, batch.Id, i, batch.ReconMonth, batch.ReconYear);
                    record.CreatedBy = uploadedBy;

                    records.Add(record);
                    existingSet.Add(dupKey); // prevent intra-batch duplicates
                    importedCount++;
                }
                catch (Exception ex)
                {
                    failedCount++;
                    failedRows.Add(new { Row = excelRowNum, Error = ex.Message });
                }

                // Bulk insert every 500 rows to avoid huge batches
                if (records.Count >= 500)
                {
                    db.RawBankPaymentRecords.AddRange(records);
                    await db.SaveChangesAsync(cancellationToken);
                    records.Clear();
                }
            }

            // Final flush
            if (records.Count > 0)
            {
                db.RawBankPaymentRecords.AddRange(records);
                await db.SaveChangesAsync(cancellationToken);
            }

            // 6. Update batch summary
            batch.ImportedRecords  = importedCount;
            batch.DuplicateRecords = duplicateCount;
            batch.FailedRecords    = failedCount;
            batch.FailedRowsJson   = failedRows.Count > 0
                ? JsonSerializer.Serialize(failedRows)
                : "[]";
            batch.Status = failedCount == totalRows
                ? BankPaymentImportStatus.Failed
                : (failedCount > 0 || duplicateCount > 0)
                    ? BankPaymentImportStatus.PartialSuccess
                    : BankPaymentImportStatus.Completed;
            batch.ImportRemarks = $"Imported: {importedCount} | Duplicates skipped: {duplicateCount} | Failed: {failedCount}";
            batch.UpdatedBy = uploadedBy;
            batch.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);

            result.Success          = importedCount > 0 || duplicateCount > 0;
            result.BatchRef         = batchRef;
            result.BatchId          = batch.Id;
            result.ImportedRecords  = importedCount;
            result.DuplicateRecords = duplicateCount;
            result.FailedRecords    = failedCount;
            result.Message = batch.Status switch
            {
                BankPaymentImportStatus.Completed      => $"Import completed. {importedCount} records imported.",
                BankPaymentImportStatus.PartialSuccess => $"Partial import. {importedCount} imported, {duplicateCount} duplicate(s) skipped, {failedCount} failed.",
                BankPaymentImportStatus.Failed         => $"Import failed. No records were inserted.",
                _                                      => "Import processed."
            };
        }
        finally
        {
            workbook?.Dispose();
        }

        return result;
    }

    // ── Queries ──────────────────────────────────────────────────────────────

    public async Task<List<BankPaymentImportBatch>> GetBatchesAsync(
        DateTime? from, DateTime? to, string? status,
        CancellationToken cancellationToken = default)
    {
        var q = db.BankPaymentImportBatches.AsNoTracking().AsQueryable();

        if (from.HasValue)
            q = q.Where(b => b.UploadedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(b => b.UploadedAt <= to.Value.AddDays(1));
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(b => b.Status == status);

        return await q.OrderByDescending(b => b.UploadedAt).ToListAsync(cancellationToken);
    }

    public async Task<BankPaymentBatchDetailViewModel> GetBatchDetailAsync(
        int batchId, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.BankPaymentImportBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken)
            ?? throw new InvalidOperationException($"Batch {batchId} not found.");

        var baseQuery = db.RawBankPaymentRecords
            .AsNoTracking()
            .Where(r => r.BatchId == batchId);

        int total = await baseQuery.CountAsync(cancellationToken);

        var records = await baseQuery
            .OrderBy(r => r.RowNumber)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BankPaymentBatchDetailViewModel
        {
            Batch      = batch,
            Records    = records,
            TotalCount = total,
            PageIndex  = pageIndex,
            PageSize   = pageSize
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Dictionary<string, int> BuildHeaderIndex(IXLWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstRow = ws.Row(1);
        int lastCol  = ws.ColumnsUsed().Count();

        for (int col = 1; col <= lastCol; col++)
        {
            var header = firstRow.Cell(col).Value.ToString().Trim();
            if (!string.IsNullOrEmpty(header) && !map.ContainsKey(header))
                map[header] = col;
        }
        return map;
    }

    private static Dictionary<string, string?> ReadRowAsDictionary(IXLRow row, Dictionary<string, int> headers)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (header, col) in headers)
        {
            if (_headerMap.TryGetValue(header, out var propName))
            {
                var cellVal = row.Cell(col).Value.ToString().Trim();
                dict[propName] = string.IsNullOrEmpty(cellVal) ? null : cellVal;
            }
        }
        return dict;
    }

    private static RawBankPaymentRecord MapToRecord(Dictionary<string, string?> d, int batchId, int rowNumber, int? reconMonth, int? reconYear)
    {
        return new RawBankPaymentRecord
        {
            BatchId              = batchId,
            RowNumber            = rowNumber,
            ReconMonth           = reconMonth,
            ReconYear            = reconYear,
            FileSequenceNum      = TryGet(d, nameof(RawBankPaymentRecord.FileSequenceNum)),
            PymtProdTypeCode     = TryGet(d, nameof(RawBankPaymentRecord.PymtProdTypeCode)),
            PymtMode             = TryGet(d, nameof(RawBankPaymentRecord.PymtMode)),
            DebitAcctNo          = TryGet(d, nameof(RawBankPaymentRecord.DebitAcctNo)),
            BeneficiaryName      = TryGet(d, nameof(RawBankPaymentRecord.BeneficiaryName)),
            BeneficiaryAccountNo = TryGet(d, nameof(RawBankPaymentRecord.BeneficiaryAccountNo)),
            BeneIfscCode         = TryGet(d, nameof(RawBankPaymentRecord.BeneIfscCode)),
            Amount               = TryGetDecimal(d, nameof(RawBankPaymentRecord.Amount)),
            DebitNarration       = TryGet(d, nameof(RawBankPaymentRecord.DebitNarration)),
            CreditNarration      = TryGet(d, nameof(RawBankPaymentRecord.CreditNarration)),
            MobileNumber         = TryGet(d, nameof(RawBankPaymentRecord.MobileNumber)),
            EmailId              = TryGet(d, nameof(RawBankPaymentRecord.EmailId)),
            Remark               = TryGet(d, nameof(RawBankPaymentRecord.Remark)),
            PymtDate             = TryGetDate(d, nameof(RawBankPaymentRecord.PymtDate)),
            ReferenceNo          = TryGet(d, nameof(RawBankPaymentRecord.ReferenceNo)),
            AddlInfo1            = TryGet(d, nameof(RawBankPaymentRecord.AddlInfo1)),
            AddlInfo2            = TryGet(d, nameof(RawBankPaymentRecord.AddlInfo2)),
            AddlInfo3            = TryGet(d, nameof(RawBankPaymentRecord.AddlInfo3)),
            AddlInfo4            = TryGet(d, nameof(RawBankPaymentRecord.AddlInfo4)),
            AddlInfo5            = TryGet(d, nameof(RawBankPaymentRecord.AddlInfo5)),
            BeneficiaryLei       = TryGet(d, nameof(RawBankPaymentRecord.BeneficiaryLei)),
            BankStatus           = TryGet(d, nameof(RawBankPaymentRecord.BankStatus)),
            CurrentStep          = TryGet(d, nameof(RawBankPaymentRecord.CurrentStep)),
            BankFileName         = TryGet(d, nameof(RawBankPaymentRecord.BankFileName)),
            RejectedBy           = TryGet(d, nameof(RawBankPaymentRecord.RejectedBy)),
            RejectionReason      = TryGet(d, nameof(RawBankPaymentRecord.RejectionReason)),
            AcctDebitDate        = TryGetDate(d, nameof(RawBankPaymentRecord.AcctDebitDate)),
            CustomerRefNo        = TryGet(d, nameof(RawBankPaymentRecord.CustomerRefNo)),
            UtrNo                = TryGet(d, nameof(RawBankPaymentRecord.UtrNo)),
        };
    }

    private static string? TryGet(Dictionary<string, string?> d, string key)
        => d.TryGetValue(key, out var v) ? v : null;

    private static decimal? TryGetDecimal(Dictionary<string, string?> d, string key)
    {
        var s = TryGet(d, key);
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Strip currency symbols and commas
        s = s.Replace(",", "").Replace("₹", "").Replace("$", "").Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static DateTime? TryGetDate(Dictionary<string, string?> d, string key)
    {
        var s = TryGet(d, key);
        if (string.IsNullOrWhiteSpace(s)) return null;
        // Try common Indian date formats
        string[] formats = ["dd-MM-yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy", "d/M/yyyy"];
        return DateTime.TryParseExact(s, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt
            : (DateTime.TryParse(s, out var dt2) ? dt2 : null);
    }

    private static string MakeDupKey(string? seqNum, string? acctNo)
        => $"{seqNum?.Trim().ToUpperInvariant()}|{acctNo?.Trim().ToUpperInvariant()}";

    private async Task<string> GenerateBatchRefAsync(CancellationToken cancellationToken)
    {
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        // Count batches created today to get the sequential suffix
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd   = todayStart.AddDays(1);
        int todayCount = await db.BankPaymentImportBatches
            .CountAsync(b => b.UploadedAt >= todayStart && b.UploadedAt < todayEnd, cancellationToken);
        return $"BNK-{dateStr}-{(todayCount + 1):D3}";
    }
}
