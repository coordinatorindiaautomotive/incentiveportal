using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

// ─────────────────────────────────────────────
// ENUMS / CONSTANTS
// ─────────────────────────────────────────────
public static class CashTxStatus
{
    public const string Draft       = "Draft";
    public const string Submitted   = "Submitted";
    public const string Verified    = "Verified";
    public const string Approved    = "Approved";
    public const string Posted      = "Posted";
    public const string Reconciled  = "Reconciled";
    public const string Rejected    = "Rejected";
    public const string Closed      = "Closed";

    public static readonly string[] Workflow =
        [Draft, Submitted, Verified, Approved, Posted, Reconciled, Closed];
}

public static class ReconStatus
{
    public const string Pending        = "Pending";
    public const string Matched        = "Matched";
    public const string Partial        = "Partial";
    public const string Mismatch       = "Mismatch";
    public const string MissingTally   = "MissingTally";
    public const string MissingPortal  = "MissingPortal";
    public const string Approved       = "Approved";
    public const string Rejected       = "Rejected";

    public const string FuzzyMatched    = "FuzzyMatched";
    public const string ExcessCash      = "ExcessCash";
    public const string ShortCash       = "ShortCash";
    public const string ManuallyMatched = "ManuallyMatched";
}

public enum ReconciliationStatus
{
    Pending,
    Matched,
    Partial,
    Mismatch,
    MissingTally,
    MissingPortal,
    Approved,
    Rejected,
    FuzzyMatched,
    ExcessCash,
    ShortCash,
    ManuallyMatched
}

public static class ExceptionSeverity
{
    public const string Critical = "Critical";
    public const string High     = "High";
    public const string Medium   = "Medium";
    public const string Low      = "Low";
}

// ─────────────────────────────────────────────
// CASH IN
// ─────────────────────────────────────────────
public sealed class CashInTransaction : AuditableEntity
{
    [MaxLength(30)]  public string TransactionNo   { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;
    public DateTime TransactionDate { get; set; }
    [MaxLength(60)]  public string ReceiptType     { get; set; } = string.Empty;
    [MaxLength(180)] public string CustomerName    { get; set; } = string.Empty;
    [MaxLength(40)]  public string DealerCode      { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [MaxLength(30)]  public string PaymentMode     { get; set; } = string.Empty;
    [MaxLength(120)] public string BankName        { get; set; } = string.Empty;
    [MaxLength(80)]  public string ReferenceNo     { get; set; } = string.Empty;
    [MaxLength(500)] public string Narration       { get; set; } = string.Empty;
    [MaxLength(260)] public string? AttachmentPath { get; set; }
    [MaxLength(20)]  public string Status          { get; set; } = CashTxStatus.Draft;
    [MaxLength(30)]  public string? TallyVoucherNo { get; set; }
    [MaxLength(20)]  public string? TallyVoucherType { get; set; }
    [MaxLength(80)]  public string? TallyLedgerName { get; set; }
    [MaxLength(80)]  public string? TallyGuid      { get; set; }
    public DateTime? TallySyncAt { get; set; }
    [MaxLength(20)]  public string? TallySyncStatus { get; set; }
    [MaxLength(200)] public string? ApprovalRemarks { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(100)] public string? ApprovedBy { get; set; }

    public ICollection<CashReconRecord> ReconRecords { get; set; } = new List<CashReconRecord>();
}

// ─────────────────────────────────────────────
// CASH OUT
// ─────────────────────────────────────────────
public sealed class CashOutTransaction : AuditableEntity
{
    [MaxLength(30)]  public string TransactionNo   { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;
    public DateTime TransactionDate { get; set; }
    [MaxLength(60)]  public string ExpenseCategory { get; set; } = string.Empty;
    [MaxLength(180)] public string VendorName      { get; set; } = string.Empty;
    [MaxLength(60)]  public string CostCenter      { get; set; } = string.Empty;
    [MaxLength(80)]  public string GlAccount       { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [MaxLength(30)]  public string PaymentMode     { get; set; } = string.Empty;
    [MaxLength(60)]  public string PaymentInstrument { get; set; } = string.Empty;
    [MaxLength(120)] public string BankName        { get; set; } = string.Empty;
    [MaxLength(80)]  public string ReferenceNo     { get; set; } = string.Empty;
    [MaxLength(500)] public string Narration       { get; set; } = string.Empty;
    [MaxLength(260)] public string? AttachmentPath { get; set; }
    [MaxLength(20)]  public string Status          { get; set; } = CashTxStatus.Draft;
    [MaxLength(30)]  public string? TallyVoucherNo { get; set; }
    [MaxLength(80)]  public string? TallyGuid      { get; set; }
    public DateTime? TallySyncAt { get; set; }
    [MaxLength(20)]  public string? TallySyncStatus { get; set; }
    [MaxLength(200)] public string? ApprovalRemarks { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(100)] public string? ApprovedBy { get; set; }

    public ICollection<CashReconRecord> ReconRecords { get; set; } = new List<CashReconRecord>();
}

// ─────────────────────────────────────────────
// RECONCILIATION RECORD
// ─────────────────────────────────────────────
public sealed class CashReconRecord : AuditableEntity
{
    [MaxLength(30)]  public string ReconRef        { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;
    public DateTime ReconDate { get; set; }
    [MaxLength(20)]  public string TransactionType { get; set; } = "CashIn"; // CashIn | CashOut
    public int? CashInId  { get; set; }
    public CashInTransaction?  CashIn  { get; set; }
    public int? CashOutId { get; set; }
    public CashOutTransaction? CashOut { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PortalAmount { get; set; }
    [MaxLength(40)]  public string? TallyVoucherNo  { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TallyAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Variance    { get; set; }
    [MaxLength(20)]  public string ReconStatus     { get; set; } = "Pending";
    [MaxLength(500)] public string? Remarks        { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(100)] public string? ApprovedBy     { get; set; }
}

// ─────────────────────────────────────────────
// EXCEPTION RECORD
// ─────────────────────────────────────────────
public sealed class CashException : AuditableEntity
{
    [MaxLength(30)]  public string ExceptionRef    { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;
    public DateTime ExceptionDate { get; set; }
    [MaxLength(60)]  public string ExceptionType   { get; set; } = string.Empty;
    [MaxLength(500)] public string Description     { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [MaxLength(20)]  public string Severity        { get; set; } = ExceptionSeverity.Medium;
    [MaxLength(20)]  public string Status          { get; set; } = "Open";  // Open|Investigating|Escalated|Resolved
    [MaxLength(100)] public string? AssignedTo     { get; set; }
    [MaxLength(500)] public string? Resolution     { get; set; }
    public DateTime? ResolvedAt { get; set; }
    [MaxLength(100)] public string? ResolvedBy     { get; set; }
}

// ─────────────────────────────────────────────
// PERIOD CONTROL
// ─────────────────────────────────────────────
public sealed class CashPeriodControl : AuditableEntity
{
    public int ControlYear  { get; set; }
    public int ControlMonth { get; set; } // 0 = Full Year
    [MaxLength(20)]  public string Status   { get; set; } = "Open"; // Open|Closed|Locked
    public DateTime? ClosedAt { get; set; }
    [MaxLength(100)] public string? ClosedBy { get; set; }
    [MaxLength(500)] public string? UnlockReason { get; set; }

    [NotMapped]
    public bool IsLocked => Status == "Locked" || Status == "Closed";
}

// ─────────────────────────────────────────────
// CASH BOOK VIEW MODEL (Excel-style Side-by-Side)
// ─────────────────────────────────────────────
public class CashBookViewModel
{
    public decimal OpeningBalance { get; set; }
    public List<CashBookRow> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal GrandTotalDebit { get; set; }
    public decimal GrandTotalCredit { get; set; }
}

public class CashBookRow
{
    public string Date { get; set; } = string.Empty;
    
    // Left side (Debit / Cash In)
    public string DebitParticulars { get; set; } = string.Empty;
    public decimal? DebitAmount { get; set; }
    
    // Right side (Credit / Cash Out)
    public string CreditParticulars { get; set; } = string.Empty;
    public decimal? CreditAmount { get; set; }
    public string CreditComments { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────
// CASH MASTER ITEM
// ─────────────────────────────────────────────
public sealed class CashMasterItem : AuditableEntity
{
    [MaxLength(60)] public string ItemType { get; set; } = string.Empty; // "ReceiptType" | "ExpenseCategory"
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

// ─────────────────────────────────────────────
// COST CENTER CASH BALANCE (Tally Synced)
// ─────────────────────────────────────────────
public sealed class CostCenterCash : AuditableEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    
    [Required, MaxLength(100)]
    public string CostCenterName { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; } // Cash In (Receipts)
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; } // Cash Out (Payments)
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal ClosingBalance { get; set; }
    
    public DateTime SyncedAt { get; set; }
}
