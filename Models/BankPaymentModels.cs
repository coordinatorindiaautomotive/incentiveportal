using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

// ─────────────────────────────────────────────
// BANK PAYMENT IMPORT STATUS CONSTANTS
// ─────────────────────────────────────────────
public static class BankPaymentImportStatus
{
    public const string Pending        = "Pending";
    public const string Completed      = "Completed";
    public const string Failed         = "Failed";
    public const string PartialSuccess = "PartialSuccess";
}

// ─────────────────────────────────────────────
// BANK PAYMENT IMPORT BATCH
// Tracks every uploaded bank payment Excel file as an immutable audit record.
// Equivalent to ImportLog for the Raw Sales table.
// ─────────────────────────────────────────────
[Table("BankPaymentImportBatches")]
public sealed class BankPaymentImportBatch : AuditableEntity
{
    /// <summary>System-generated unique batch reference — e.g. BNK-20260713-001</summary>
    [Required, MaxLength(30)]
    public string BatchRef { get; set; } = string.Empty;

    /// <summary>Original file name as uploaded by the user.</summary>
    [Required, MaxLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Portal username who triggered the upload.</summary>
    [Required, MaxLength(100)]
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the upload action.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Total rows found in the Excel file.</summary>
    public int TotalRecords { get; set; }

    /// <summary>Rows successfully inserted into RawBankPaymentRecords.</summary>
    public int ImportedRecords { get; set; }

    /// <summary>Rows skipped because they matched an existing FileSequenceNum + UtrNo pair.</summary>
    public int DuplicateRecords { get; set; }

    /// <summary>Rows that failed parsing or validation.</summary>
    public int FailedRecords { get; set; }

    /// <summary>Overall batch status: Pending | Completed | Failed | PartialSuccess</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = BankPaymentImportStatus.Pending;

    /// <summary>Free-text notes recorded during import processing.</summary>
    [MaxLength(1000)]
    public string? ImportRemarks { get; set; }

    /// <summary>True when a SuperAdmin explicitly forced re-import of a duplicate file.</summary>
    public bool IsForcedReimport { get; set; }

    /// <summary>Username of the SuperAdmin who authorized the forced re-import.</summary>
    [MaxLength(100)]
    public string? ForcedBy { get; set; }

    /// <summary>Error details as JSON array for rows that failed.</summary>
    public string FailedRowsJson { get; set; } = "[]";

    /// <summary>Target Month for Reconciliation (1-12)</summary>
    public int? ReconMonth { get; set; }

    /// <summary>Target Year for Reconciliation (e.g. 2026)</summary>
    public int? ReconYear { get; set; }

    public ICollection<RawBankPaymentRecord> Records { get; set; } = new List<RawBankPaymentRecord>();
}

// ─────────────────────────────────────────────
// RAW BANK PAYMENT RECORD  (SSOT — INSERT-ONLY)
//
// Stores every row from an uploaded bank payment Excel file exactly as
// received from the bank — no field is ever modified after insert.
// This table is the Single Source of Truth for all payment reconciliation.
//
// Only INSERT operations are permitted. The service layer enforces this.
// ─────────────────────────────────────────────
[Table("RawBankPaymentRecords")]
public sealed class RawBankPaymentRecord : AuditableEntity
{
    // ── Batch reference ────────────────────────────────────────────
    public int BatchId { get; set; }
    public BankPaymentImportBatch Batch { get; set; } = default!;

    /// <summary>1-based row index in the original Excel file.</summary>
    public int RowNumber { get; set; }

    // ── Reconciliation Period (Month / Year) persisted directly inside record ──
    public int? ReconMonth { get; set; }
    public int? ReconYear { get; set; }

    // ── Original bank columns — stored verbatim, never modified ───
    [MaxLength(50)]  public string? FileSequenceNum      { get; set; }
    [MaxLength(50)]  public string? PymtProdTypeCode     { get; set; }
    [MaxLength(50)]  public string? PymtMode             { get; set; }
    [MaxLength(50)]  public string? DebitAcctNo          { get; set; }
    [MaxLength(200)] public string? BeneficiaryName      { get; set; }
    [MaxLength(50)]  public string? BeneficiaryAccountNo { get; set; }
    [MaxLength(20)]  public string? BeneIfscCode         { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Amount { get; set; }

    [MaxLength(500)] public string? DebitNarration   { get; set; }
    [MaxLength(500)] public string? CreditNarration  { get; set; }
    [MaxLength(20)]  public string? MobileNumber     { get; set; }
    [MaxLength(180)] public string? EmailId          { get; set; }
    [MaxLength(500)] public string? Remark           { get; set; }
    public DateTime?               PymtDate         { get; set; }
    [MaxLength(80)]  public string? ReferenceNo      { get; set; }
    [MaxLength(200)] public string? AddlInfo1        { get; set; }
    [MaxLength(200)] public string? AddlInfo2        { get; set; }
    [MaxLength(200)] public string? AddlInfo3        { get; set; }
    [MaxLength(200)] public string? AddlInfo4        { get; set; }
    [MaxLength(200)] public string? AddlInfo5        { get; set; }
    [MaxLength(40)]  public string? BeneficiaryLei   { get; set; }
    [MaxLength(50)]  public string? BankStatus       { get; set; }
    [MaxLength(80)]  public string? CurrentStep      { get; set; }
    [MaxLength(260)] public string? BankFileName     { get; set; }
    [MaxLength(100)] public string? RejectedBy       { get; set; }
    [MaxLength(500)] public string? RejectionReason  { get; set; }
    public DateTime?               AcctDebitDate     { get; set; }
    [MaxLength(80)]  public string? CustomerRefNo    { get; set; }

    /// <summary>
    /// UTR Number — the primary deduplication key alongside FileSequenceNum.
    /// A record is considered a duplicate if (FileSequenceNum + UtrNo) already exists.
    /// </summary>
    [MaxLength(80)]  public string? UtrNo            { get; set; }
}
