using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IncentivePortal.Models;

public abstract class AuditableEntity
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(100)] public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    [MaxLength(100)] public string? UpdatedBy { get; set; }
}

public sealed class Role : AuditableEntity
{
    [MaxLength(60)] public string Name { get; set; } = string.Empty;
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public sealed class User : AuditableEntity
{
    [MaxLength(80)] public string UserName { get; set; } = string.Empty;
    [MaxLength(160)] public string Email { get; set; } = string.Empty;
    [MaxLength(256)] public string PasswordHash { get; set; } = string.Empty;
    [MaxLength(256)] public string PasswordSalt { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

public sealed class UserRole
{
    public int UserId { get; set; }
    public User User { get; set; } = default!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = default!;
}

public sealed class Branch : AuditableEntity
{
    [MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(120)] public string Region { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = "Active";

    [MaxLength(20)] public string BranchType { get; set; } = string.Empty; // MW, RO, AW
    [MaxLength(40)] public string Consignee { get; set; } = string.Empty;
    [MaxLength(100)] public string Incharge { get; set; } = string.Empty;
    [MaxLength(30)] public string MobileNo { get; set; } = string.Empty;
    [MaxLength(120)] public string EmailID { get; set; } = string.Empty;
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    [MaxLength(30)] public string OpeningYear { get; set; } = string.Empty;
    [MaxLength(30)] public string Area { get; set; } = string.Empty;
    [MaxLength(30)] public string Longitude { get; set; } = string.Empty;
    [MaxLength(30)] public string Latitude { get; set; } = string.Empty;

    [MaxLength(100)] public string AllowedCategories { get; set; } = "AA,M";
    [MaxLength(200)] public string AllowedPartyTypes { get; set; } = "INDEPENDENT WORKSHOP";
    [MaxLength(20)] public string TallyOutletCode { get; set; } = string.Empty;

    [InverseProperty("Branch")]
    public ICollection<Party> Parties { get; set; } = new List<Party>();
}

public sealed class Party : AuditableEntity
{
    [MaxLength(40)] public string PartyCode { get; set; } = string.Empty;
    [MaxLength(180)] public string PartyName { get; set; } = string.Empty;
    [MaxLength(20)] public string GST { get; set; } = string.Empty;
    [MaxLength(20)] public string Mobile { get; set; } = string.Empty;
    [MaxLength(500)] public string Address { get; set; } = string.Empty;
    [MaxLength(40)] public string DealerType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(9,4)")] public decimal FixedIncentivePercent { get; set; }
    [MaxLength(40)] public string OriginalPartyCode { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = "Active";
    
    // Effective/Manual Location
    public int BranchId { get; set; }
    [ForeignKey("BranchId")]
    public Branch Branch { get; set; } = default!;
    
    // Auto-Detected Base Location (calculated from history)
    public int? AutoBaseBranchId { get; set; }
    [ForeignKey("AutoBaseBranchId")]
    public Branch? AutoBaseBranch { get; set; }
    
    // Flag to override Auto-Detection
    public bool IsManuallyMapped { get; set; }

    public ICollection<BankDetail> BankDetails { get; set; } = new List<BankDetail>();
}

/// <summary>
/// Governor Engine Mapping Master — maps alternative/alias party codes
/// (as they appear in uploaded Excel files) to their canonical Original Code.
/// Rule: RawRecord.ConsPartyCode is NEVER modified; the resolved code is
/// stamped into RawRecord.OriginalCode by the Governor Engine at import time.
/// </summary>
[Table("PartyCodeMappings")]
public sealed class PartyCodeMapping : AuditableEntity
{
    /// <summary>The canonical/master party code as registered in the Party master.</summary>
    [Required, MaxLength(40)]
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>The alias/alternative code that may appear in uploaded sales files.</summary>
    [Required, MaxLength(40)]
    public string AlternativeCode { get; set; } = string.Empty;

    /// <summary>Optional description — e.g. which source system uses this alias.</summary>
    [MaxLength(300)]
    public string? Notes { get; set; }

    /// <summary>Inactive mappings are excluded from resolution but kept for audit trail.</summary>
    public bool IsActive { get; set; } = true;
}

public sealed class BankDetail : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    [MaxLength(160)] public string AccountHolder { get; set; } = string.Empty;
    [MaxLength(30)] public string AccountNumber { get; set; } = string.Empty;
    [MaxLength(15)] public string IFSC { get; set; } = string.Empty;
    [MaxLength(120)] public string BankName { get; set; } = string.Empty;
    [MaxLength(120)] public string BranchName { get; set; } = string.Empty;
    [MaxLength(20)] public string ApprovalStatus { get; set; } = "Pending";
    public bool IsPrimary { get; set; } = true;
    [MaxLength(20)] public string PAN { get; set; } = string.Empty;
    [MaxLength(20)] public string Mobile { get; set; } = string.Empty;
}

public sealed class BankApprovalRequest : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    public int? BankDetailId { get; set; }
    public BankDetail? BankDetail { get; set; }
    [MaxLength(40)] public string RequestType { get; set; } = "Create";
    [MaxLength(20)] public string Status { get; set; } = "Pending";
    public string OldJson { get; set; } = "{}";
    public string NewJson { get; set; } = "{}";
    [MaxLength(500)] public string? Remarks { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(100)] public string? ApprovedBy { get; set; }
}

public sealed class IncentiveScheme : AuditableEntity
{
    [MaxLength(120)] public string Name { get; set; } = string.Empty;
    public int SchemeMonth { get; set; }
    public int SchemeYear { get; set; }
    public int Version { get; set; } = 1;
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public bool IsLocked { get; set; }

    [NotMapped]
    public bool IsActive { get; set; } = true;

    public ICollection<IncentiveSchemeDetail> Details { get; set; } = new List<IncentiveSchemeDetail>();
}

public sealed class IncentiveSchemeDetail : AuditableEntity
{
    public int IncentiveSchemeId { get; set; }
    public IncentiveScheme IncentiveScheme { get; set; } = default!;
    [Column(TypeName = "decimal(18,2)")] public decimal MinAchievementPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal MaxAchievementPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? FixedAmount { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal? Percentage { get; set; }
    [MaxLength(250)] public string RuleName { get; set; } = string.Empty;
}


public sealed class AuditLog
{
    public long Id { get; set; }
    [MaxLength(120)] public string EntityName { get; set; } = string.Empty;
    [MaxLength(40)] public string EntityId { get; set; } = string.Empty;
    [MaxLength(40)] public string Action { get; set; } = string.Empty;
    public string OldValue { get; set; } = "{}";
    public string NewValue { get; set; } = "{}";
    [MaxLength(100)] public string ChangedBy { get; set; } = "system";
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(60)] public string? IpAddress { get; set; }
}

public sealed class ImportLog : AuditableEntity
{
    [MaxLength(80)] public string ImportType { get; set; } = "MonthlySales";
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Previewed";
    public string ErrorJson { get; set; } = "[]";

    // Batching & Versioning Extensions
    public bool IsHistorical { get; set; }
    public int Year { get; set; }
    public int Month { get; set; } // 0 if full-year historical
    public int VersionNumber { get; set; } = 1;
    [MaxLength(500)] public string? ChangeReason { get; set; }
    public int? PreviousImportLogId { get; set; }
    public ImportLog? PreviousImportLog { get; set; }
    public DateTime? LockedAt { get; set; }
    [MaxLength(100)] public string? LockedBy { get; set; }
}

public sealed class DealerMonthlyPerformance : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    public int Month { get; set; }
    public int Year { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalSales { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TotalDiscount { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal SlabPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IncentiveEarned { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Outstanding { get; set; }
    [MaxLength(100)] public string GrowthTrend { get; set; } = string.Empty;
}

public sealed class IncentiveSummary : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    public int Month { get; set; }
    public int Year { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentSale { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal CurrentSlabPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentIncentive { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NextSlabTarget { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AdditionalPurchaseRequired { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NextIncentive { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ForecastedIncentive { get; set; }
}

public sealed class DealerSlabProgress : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    public int Month { get; set; }
    public int Year { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentSale { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal NextSlabPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NextSlabTarget { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal ProgressPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RemainingAmount { get; set; }
}

public sealed class DealerGrowthAnalytics : AuditableEntity
{
    public int PartyId { get; set; }
    public Party Party { get; set; } = default!;
    public int Month { get; set; }
    public int Year { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalesCurrent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalesPriorMonth { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal SalesPriorYearSamePeriod { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IncentiveCurrent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IncentivePriorMonth { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal IncentivePriorYearSamePeriod { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal SalesGrowthMoM { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal SalesGrowthYoY { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal IncentiveGrowthMoM { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal IncentiveGrowthYoY { get; set; }
}

public sealed class MonthLock : AuditableEntity
{
    public int LockYear { get; set; }
    public int LockMonth { get; set; } // 0 = Full Year Lock (Historical)
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    [MaxLength(100)] public string? LockedBy { get; set; }
    [MaxLength(500)] public string? UnlockReason { get; set; }
}

[Table("IncentivePeriodLocks")]
public sealed class IncentivePeriodLock : AuditableEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    [Required, MaxLength(40)] public string BranchCode { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string PartCategoryCode { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string IncentiveSource { get; set; } = string.Empty; // "Calculator" or "Manual Upload"
    [Required, MaxLength(50)] public string LockStatus { get; set; } = "Locked"; // "Locked", "Unlocked"
    [MaxLength(100)] public string? LockedBy { get; set; }
    public DateTime? LockedDate { get; set; }
    [MaxLength(100)] public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    [MaxLength(500)] public string? UnlockReason { get; set; }
    [MaxLength(500)] public string? UnlockRemarks { get; set; }
}


public sealed class PartyExecutiveMapping : AuditableEntity
{
    [Required, MaxLength(40)] public string PartyCode { get; set; } = string.Empty;
    [Required, MaxLength(180)] public string PartyName { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string ExecutiveCode { get; set; } = string.Empty;
    [Required, MaxLength(160)] public string ExecutiveName { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string BranchCode { get; set; } = string.Empty;
}

public sealed class Announcement : AuditableEntity
{
    [Required, MaxLength(500)] public string Message { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [MaxLength(20)] public string Severity { get; set; } = "info"; // info, warning, danger, success
}

public sealed class PortalSetting : AuditableEntity
{
    [MaxLength(100)] public string Key { get; set; } = string.Empty;
    [MaxLength(500)] public string Value { get; set; } = string.Empty;
}

public sealed class CategorySalesAggregate : AuditableEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    [MaxLength(40)] public string MonthYear { get; set; } = string.Empty;
    [MaxLength(20)] public string Quarter { get; set; } = string.Empty;
    [MaxLength(60)] public string PartyType { get; set; } = string.Empty;
    [MaxLength(20)] public string PartCategoryCode { get; set; } = string.Empty;
    [MaxLength(40)] public string Loc { get; set; } = string.Empty;
    [MaxLength(20)] public string DealerSubType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal NetSales { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetDdl { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Discount { get; set; }
    public int Transactions { get; set; }
}

[Table("Raw")]
public sealed class RawRecord : AuditableEntity
{
    [MaxLength(40)] public string? DealerSubType { get; set; }
    [MaxLength(180)] public string? Consignee { get; set; }
    [MaxLength(40)] public string? DealerCode { get; set; }
    [MaxLength(40)] public string? Loc { get; set; }
    [MaxLength(20)] public string? PartCategoryCode { get; set; }
    [MaxLength(40)] public string? FiscalYear { get; set; }
    [MaxLength(20)] public string? Quarter { get; set; }
    [MaxLength(20)] public string? Month { get; set; }
    [MaxLength(40)] public string? MonthYear { get; set; }
    [MaxLength(40)] public string? ConsPartyCode { get; set; }
    [MaxLength(180)] public string? ConsPartyName { get; set; }
    [MaxLength(60)] public string? PartyType { get; set; }
    [MaxLength(80)] public string? DocumentNum { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetRetailSelling { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DiscountAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetRetailDdl { get; set; }
    [MaxLength(40)] public string? OriginalCode { get; set; }
    public int? ImportLogId { get; set; }
    public ImportLog? ImportLog { get; set; }
    public int? RowNumber { get; set; }
    public int? MonthNumber { get; set; }
    public int? YearNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AchievementPercent { get; set; }

    [MaxLength(100)] public string? PartNum { get; set; }
    [MaxLength(100)] public string? RootPartNum { get; set; }
    public int? Day { get; set; }
    public int? NetRetailQty { get; set; }
}

/// <summary>Tracks each Excel batch uploaded via the External Incentive Upload module.</summary>
[Table("ExternalIncentiveUploads")]
public sealed class ExternalIncentiveUpload : AuditableEntity
{
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    [MaxLength(50)] public string MonthLabel { get; set; } = string.Empty; // e.g. "May-26"
    public int TotalRows { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Completed";
    [MaxLength(500)] public string? Remarks { get; set; }
    public ICollection<ExternalIncentiveRecord> Records { get; set; } = new List<ExternalIncentiveRecord>();
}

/// <summary>Stores individual rows from externally uploaded incentive Excel files.</summary>
[Table("ExternalIncentiveRecords")]
public sealed class ExternalIncentiveRecord : AuditableEntity
{
    public int UploadId { get; set; }
    public ExternalIncentiveUpload Upload { get; set; } = default!;

    public int Month { get; set; }
    public int Year { get; set; }
    [MaxLength(50)]  public string MonthLabel { get; set; } = string.Empty;       // "May-26"
    [MaxLength(40)]  public string ConsPartyCode { get; set; } = string.Empty;
    [MaxLength(180)] public string ConsPartyName { get; set; } = string.Empty;
    [MaxLength(40)]  public string Location { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal NetRetailSelling { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal DiscountAmount { get; set; }
    [MaxLength(20)]  public string Slab { get; set; } = string.Empty;              // e.g. "5%"
    [Column(TypeName = "decimal(18,2)")] public decimal Incentive { get; set; }
}


public sealed class TdsRule : AuditableEntity
{
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AnnualThreshold { get; set; }
    [Column(TypeName = "decimal(6,4)")] public decimal RateWithPan { get; set; }
    [Column(TypeName = "decimal(6,4)")] public decimal RateNoPan { get; set; }
    [MaxLength(20)] public string Section { get; set; } = "194H";
    [MaxLength(500)] public string? Notes { get; set; }
}

public sealed class ColumnMappingRule : AuditableEntity
{
    [MaxLength(120)] public string ExcelHeader { get; set; } = string.Empty;
    [MaxLength(120)] public string PortalField { get; set; } = string.Empty;
    [MaxLength(60)] public string UploadContext { get; set; } = "MonthlySales";
    public bool IsActive { get; set; } = true;
    [MaxLength(300)] public string? Notes { get; set; }
}

public sealed class OutstandingRule : AuditableEntity
{
    [MaxLength(80)] public string Name { get; set; } = string.Empty;
    [Column(TypeName = "decimal(5,4)")] public decimal DeductionRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ThresholdAmount { get; set; }
    [MaxLength(200)] public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; } = 1;
}

public sealed class NotificationSetting : AuditableEntity
{
    [MaxLength(40)] public string Provider { get; set; } = "SMTP";
    [MaxLength(200)] public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool SmtpUseSsl { get; set; } = true;
    [MaxLength(160)] public string SmtpUser { get; set; } = string.Empty;
    [MaxLength(256)] public string SmtpPassEncrypted { get; set; } = string.Empty;
    [MaxLength(160)] public string FromEmail { get; set; } = string.Empty;
    [MaxLength(120)] public string FromName { get; set; } = string.Empty;
    [MaxLength(80)] public string SmsApiKey { get; set; } = string.Empty;
    [MaxLength(20)] public string SmsSenderId { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; } = false;
    public bool SmsEnabled { get; set; } = false;
}

public sealed class RolePermission : AuditableEntity
{
    [MaxLength(60)] public string RoleName { get; set; } = string.Empty;
    [MaxLength(80)] public string Module { get; set; } = string.Empty;
    [MaxLength(40)] public string Action { get; set; } = string.Empty;
    public bool IsAllowed { get; set; } = true;
}

public sealed class TallyIntegrationSetting : AuditableEntity
{
    [MaxLength(200)] public string BaseUrl { get; set; } = "http://localhost:9000";
    public int Port { get; set; } = 9000;
    [MaxLength(80)] public string CompanyName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public int TimeoutSeconds { get; set; } = 30;
    [MaxLength(100)] public string? Username { get; set; }
    [MaxLength(256)] public string? PasswordEncrypted { get; set; }
    public DateTime? LastSyncAt { get; set; }
    [MaxLength(40)] public string LastSyncStatus { get; set; } = "Never";
}

public sealed class ReportColumnConfig : AuditableEntity
{
    [MaxLength(60)] public string ReportName { get; set; } = string.Empty;
    [MaxLength(80)] public string ColumnKey { get; set; } = string.Empty;
    [MaxLength(120)] public string DisplayName { get; set; } = string.Empty;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    [MaxLength(30)] public string? Format { get; set; }
}

public sealed class HelpText : AuditableEntity
{
    [MaxLength(100)] public string FieldKey { get; set; } = string.Empty;
    [MaxLength(500)] public string Text { get; set; } = string.Empty;
    [MaxLength(60)] public string Page { get; set; } = string.Empty;
}

public sealed class IncentivePeriod : AuditableEntity
{
    public int Month { get; set; }
    public int Year { get; set; }
    [Required, MaxLength(50)] public string SourceType { get; set; } = "Dynamic"; // Dynamic, PreCalculated, PayoutImport
    [Required, MaxLength(50)] public string Status { get; set; } = "Draft"; // Draft, Validated, Approved, Locked
    public bool LockedFlag { get; set; }
    [MaxLength(100)] public string? LockedBy { get; set; }
    public DateTime? LockedDate { get; set; }
}

[Table("ssincentives")]
public sealed class SsIncentive : AuditableEntity, IBranchIsolated
{
    public int Month { get; set; }
    public int Year { get; set; }
    [MaxLength(50)] public string MonthLabel { get; set; } = string.Empty;
    [MaxLength(40)] public string PartyCode { get; set; } = string.Empty;
    [MaxLength(180)] public string PartyName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal SaleValue { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal SlabPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal OnBillDiscount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal AchievementPercent { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal GrossIncentive { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TdsAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal NetTransferAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal TransferredAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Outstanding { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? OutstandingLess7Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding7To14Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding14To21Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding21To28Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding28To35Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding35To50Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding50To80Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? OutstandingMore80Days { get; set; }
    public DateTime? ProcessingDate { get; set; }
    public DateTime? PaymentDate { get; set; }
    [MaxLength(50)] public string PaymentStatus { get; set; } = "Pending";
    [MaxLength(100)] public string? UTRNumber { get; set; }
    [MaxLength(20)] public string PartCategoryCode { get; set; } = string.Empty;
    [MaxLength(40)] public string SourceLocation { get; set; } = string.Empty;
    [MaxLength(50)] public string BankAccountNumber { get; set; } = string.Empty;
    [MaxLength(50)] public string IFSC { get; set; } = string.Empty;
    [MaxLength(180)] public string BeneficiaryName { get; set; } = string.Empty;
    
    // Unified Engine fields
    [MaxLength(50)] public string Mode { get; set; } = string.Empty; // "PreCalculated" or "Dynamic"
    [MaxLength(50)] public string Status { get; set; } = "Draft"; // "Draft", "Pending Approval", "Approved", "Posted"
    public bool IsEdited { get; set; }
    [MaxLength(100)] public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }

    public int? ImportLogId { get; set; }
    public ImportLog? ImportLog { get; set; }

    [MaxLength(200)]
    public string? TdsNote { get; set; }

    [MaxLength(50)] public string? IncentiveType { get; set; }
    [MaxLength(100)] public string? ApplicableSlab { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

[Table("BankStatementRecords")]
public sealed class BankStatementRecord : AuditableEntity
{
    public int Month { get; set; }
    public int Year { get; set; }
    [MaxLength(260)] public string FileName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    
    [MaxLength(180)] public string? BeneficiaryName { get; set; }
    [MaxLength(50)] public string? AccountNumber { get; set; }
    [MaxLength(50)] public string? IFSC { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
    [MaxLength(50)] public string? Status { get; set; }
    [MaxLength(100)] public string? UTR { get; set; }
    public DateTime? PaymentDate { get; set; }
    [MaxLength(100)] public string? PartyCode { get; set; }
    
    public bool IsReconciled { get; set; }
    public string RawRowJson { get; set; } = "{}";
    
    public int? ImportLogId { get; set; }
    public ImportLog? ImportLog { get; set; }
}

[Table("DealerOutstandings")]
public sealed class DealerOutstanding : AuditableEntity
{
    public int Month { get; set; }
    public int Year { get; set; }
    [Required, MaxLength(50)] public string MonthLabel { get; set; } = string.Empty;
    [Required, MaxLength(40)] public string PartyCode { get; set; } = string.Empty;
    [Required, MaxLength(180)] public string PartyName { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal Outstanding { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? OutstandingLess7Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding7To14Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding14To21Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding21To28Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding28To35Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding35To50Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Outstanding50To80Days { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? OutstandingMore80Days { get; set; }
    public DateTime? SyncedAt { get; set; }
}

// ── Asset Register ───────────────────────────────────────────────────────────
public sealed class AssetItem : AuditableEntity
{
    public int BranchId { get; set; }
    public Branch Branch { get; set; } = default!;

    [MaxLength(40)]  public string AssetCode            { get; set; } = string.Empty; // e.g. ALW-001
    [MaxLength(30)]  public string Category             { get; set; } = string.Empty; // Furniture / IT Equipment / Vehicle / Office Equipment / Other
    [MaxLength(200)] public string Name                 { get; set; } = string.Empty;
    [MaxLength(500)] public string Description          { get; set; } = string.Empty;
    [MaxLength(60)]  public string Manufacturer         { get; set; } = string.Empty;
    [MaxLength(60)]  public string ModelNumber          { get; set; } = string.Empty;
    [MaxLength(60)]  public string SerialNumber         { get; set; } = string.Empty;

    public DateTime? PurchaseDate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal PurchaseCost { get; set; }
    [MaxLength(120)] public string Vendor               { get; set; } = string.Empty;
    [MaxLength(40)]  public string InvoiceNumber        { get; set; } = string.Empty;

    [Column(TypeName = "decimal(9,4)")] public decimal DepreciationRatePercent { get; set; } // annual %
    [Column(TypeName = "decimal(18,2)")] public decimal CurrentValue           { get; set; }

    [MaxLength(30)]   public string Condition           { get; set; } = "Good";   // New / Good / Fair / Poor / Scrapped
    [MaxLength(30)]   public string Status              { get; set; } = "Active"; // Active / In Repair / Disposed / Written Off
    [MaxLength(200)]  public string AssetLocation       { get; set; } = string.Empty; // physical spot inside the branch
    [MaxLength(100)]  public string AssignedTo          { get; set; } = string.Empty;
    public DateTime? WarrantyExpiryDate { get; set; }
    [MaxLength(1000)] public string Remarks             { get; set; } = string.Empty;
}

[Table("DealerTargets")]
public sealed class DealerTarget : AuditableEntity
{
    public int Month { get; set; }
    public int Year { get; set; }
    [MaxLength(40)] public string PartyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal SystemSuggestedTarget { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? AdminDefinedTarget { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal FinalTarget { get; set; }
}

// ── Governor Engine — Product Code Mapping Master ─────────────────────────────
/// <summary>
/// Maps an alternative/uploaded product part number to its canonical Original Code.
/// Mirrors the pattern of PartyCodeMappings but for product-level resolution.
/// The Governor reads this master once per run; the RawRecord is never modified.
/// </summary>
[Table("ProductCodeMappings")]
public sealed class ProductCodeMapping : AuditableEntity
{
    /// <summary>The canonical product part number as registered in the Product master.</summary>
    [Required, MaxLength(100)] public string OriginalCode { get; set; } = string.Empty;

    /// <summary>The alternative/alias part number that may appear in uploaded sales files.</summary>
    [Required, MaxLength(100)] public string AlternativeCode { get; set; } = string.Empty;
    [MaxLength(200)] public string? Description { get; set; }

    /// <summary>Inactive mappings are excluded from resolution but kept for audit.</summary>
    public bool IsActive { get; set; } = true;
}

[Table("CustomReportLayouts")]
public sealed class CustomReportLayout : AuditableEntity
{
    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string ReportType { get; set; } = "Tabular"; // Tabular, Pivot, Summary
    
    public string SelectedFieldsJson { get; set; } = "[]"; 
    public string PivotRowsJson { get; set; } = "[]";      
    public string PivotColumnsJson { get; set; } = "[]";   
    public string PivotValuesJson { get; set; } = "[]";    
    public string FiltersJson { get; set; } = "[]";         
    public string SortsJson { get; set; } = "[]";           
    public string GroupsJson { get; set; } = "[]";          
}

[Table("ReportSchedules")]
public sealed class ReportSchedule : AuditableEntity
{
    public int LayoutId { get; set; }
    public CustomReportLayout Layout { get; set; } = default!;
    
    [Required, MaxLength(300)]
    public string RecipientEmails { get; set; } = string.Empty;
    [Required, MaxLength(20)]
    public string Frequency { get; set; } = "Daily"; 
    [Required, MaxLength(50)]
    public string CronExpression { get; set; } = "0 8 * * *"; 
    public bool IsActive { get; set; } = true;
    [MaxLength(100)]
    public string? LastRunJobId { get; set; }
}

/// <summary>
/// Analytics cache table — stores the computed Primary Branch for each party.
/// Derived from the Raw sales table by finding the branch code (Loc) with the
/// maximum number of transactions for each party. Raw is NEVER modified.
/// Rebuilt after each import and on startup.
/// </summary>
[Table("PartyPrimaryBranch")]
public sealed class PartyPrimaryBranch : AuditableEntity
{
    /// <summary>Canonical party code (OriginalCode or ConsPartyCode from Raw).</summary>
    [Required, MaxLength(40)]
    public string PartyCode { get; set; } = string.Empty;

    /// <summary>Branch code (Loc) from which this party has made the most purchases.</summary>
    [Required, MaxLength(40)]
    public string PrimaryBranchCode { get; set; } = string.Empty;

    /// <summary>Transaction count at the primary branch (determines dominance).</summary>
    public int TransactionCount { get; set; }

    /// <summary>Total NetRetailSelling at the primary branch.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalSales { get; set; }

    /// <summary>UTC timestamp of the last recomputation.</summary>
    public DateTime LastRefreshedAt { get; set; } = DateTime.UtcNow;
}



