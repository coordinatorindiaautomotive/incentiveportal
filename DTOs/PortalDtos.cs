using System.ComponentModel.DataAnnotations;
using IncentivePortal.Models;

namespace IncentivePortal.DTOs;

public sealed record LoginRequest([Required] string UserName, [Required] string Password);
public sealed record LoginResponse(bool Succeeded, string? Token, string Message);
public sealed record DashboardSummary(
    decimal TotalIncentive,
    int PendingApprovals,
    decimal TransferPending,
    int ActiveParties,
    decimal TotalSales,
    decimal TotalDiscount,
    int ImportedRows,
    int PendingTransfers,
    int ReconciledTransfers,
    string LastImportFile,
    DateTime? LastImportAt,
    decimal SalesGrowthMonthOnMonth,
    decimal IncentiveGrowthMonthOnMonth,
    decimal SalesGrowthYearOnYear,
    decimal IncentiveGrowthYearOnYear,
    IReadOnlyList<PeriodComparison> PerformanceComparisons,
    IReadOnlyList<PartyPerformanceDto> TopDealers,
    IReadOnlyList<PartyPerformanceDto> WeakDealers,
    IReadOnlyList<BranchPerformanceDto> BranchPerformance,
    IReadOnlyList<DashboardWorkItem> WorkQueue,
    IReadOnlyList<BranchRankingDto> BranchRankings,
    IReadOnlyList<SmartInsightDto> SmartInsights,
    DashboardChartDataDto ChartData,
    decimal BudgetVsActualPercent,
    decimal AvgIncentivePercent,
    int TotalHistoricalRecords,
    int CurrentYearRecords,
    string CurrentMonthUploadStatus,
    decimal PaidIncentives,
    decimal PendingIncentives);
public sealed record BranchPerformanceDto(string BranchName, decimal Sales, decimal Incentive);
public sealed record PartyPerformanceDto(string PartyCode, string PartyName, decimal Sales, decimal Incentive, decimal SlabPercent, decimal GrowthMoM, decimal GrowthYoY, decimal ProgressPercent);
public sealed record PeriodComparison(string Metric, decimal CurrentValue, decimal PreviousValue, decimal GrowthPercent, string Detail);
public sealed record BranchAnalyticsDto(string BranchName, decimal TotalSales, decimal TotalIncentive, decimal GrowthPercent, IReadOnlyList<PartyPerformanceDto> TopDealers, IReadOnlyList<PartyPerformanceDto> WeakDealers, decimal SlabAchievementPercent, IReadOnlyList<SmartInsightDto> SmartInsights);
public sealed record PartySummaryDto(int PartyId, string PartyCode, string PartyName, decimal CurrentSale, decimal CurrentSlabPercent, decimal CurrentIncentive, decimal NextSlabPercent, decimal AdditionalPurchaseRequired, decimal NextIncentive, decimal GrowthMoM, decimal GrowthYoY, decimal ProgressPercent, IReadOnlyList<IncentiveSchemeSlabDto> ActiveSlabs);
public sealed record DashboardWorkItem(string Title, string Detail, string Severity, string Url);
public sealed record PartyDto(int Id, string PartyCode, string PartyName, string GST, string Mobile, string Address, int BranchId, string BranchName, string DealerType, string Status, decimal FixedIncentivePercent);
public sealed record BranchDto(int Id, string Code, string Name, string Region, string Status);
public sealed record BankDetailRequest(int PartyId, string AccountHolder, string AccountNumber, string IFSC, string BankName, string BranchName, string PAN, string Mobile);
public sealed record SchemeDetailRequest(decimal MinAchievementPercent, decimal MaxAchievementPercent, decimal? FixedAmount, decimal? Percentage, string RuleName);
public sealed record SchemeRequest(string Name, int SchemeMonth, int SchemeYear, DateTime EffectiveFrom, DateTime EffectiveTo, IReadOnlyList<SchemeDetailRequest> Details);
public sealed record SalesImportRow(
    int RowNumber,
    int Month,
    int Year,
    string PartyCode,
    string PartyName,
    string Location,
    decimal SaleValue,
    decimal Discount,
    decimal SlabPercent,
    decimal FileIncentive,
    decimal CalculatedIncentive,
    decimal AchievementPercent,
    string ImportMode,
    string? Error,
    string OriginalPartyCode,
    string? PartyType = null,
    string? PartCategoryCode = null,
    int? TotalRawRows = null,
    string? DealerSubType = null,
    string? Consignee = null,
    string? FiscalYear = null,
    string? Quarter = null,
    string? DocumentNum = null,
    string? Remarks = null,
    decimal? NetRetailDdl = null,
    int? Day = null,
    string? DealerCode = null,
    decimal? TransferAmount = null,
    string? PaymentStatus = null,
    string? Utr = null,
    DateTime? PaymentDate = null,
    string? BeneficiaryName = null,
    string? BankAccountNumber = null,
    string? IFSC = null,
    string? ValidationStatus = "Valid",
    string ResolvedPartyCode = "",
    string? PartNum = null,
    string? RootPartNum = null,
    int? NetRetailQty = null);

public sealed record ImportRowResult(
    int RowNumber,
    string PartyCode,
    int Month,
    int Year,
    decimal SaleValue,
    string ValidationStatus,
    string? ErrorMessage);

public sealed class ImportSummary
{
    public int TotalRows { get; set; }
    public int Committed { get; set; }
    public int Skipped { get; set; }
    public int DeletedRecords { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public ImportLog Log { get; set; } = default!;
}

public sealed record CalculationResult(string PartyCode, decimal GrossIncentive, decimal AdjustedAmount, decimal TransferAmount);

/// <summary>
/// Branch-level filter rules for selective incentive calculation. 
/// Passed from the UI governance console to restrict which branches/categories/party-types get calculated.
/// </summary>
public sealed record BranchCalcRule(string Location, string AllowedCategories, string AllowedPartyTypes);

public sealed class IncentiveRegisterFilter
{
    public string? PartyCode { get; set; }
    public List<string> SelectedPartyCodes { get; set; } = new();
    public int? Month { get; set; }
    public int? Year { get; set; }
    public List<string> SelectedPeriods { get; set; } = new();
    public List<string> SelectedPaymentPeriods { get; set; } = new();
}

public sealed class IncentiveRegisterViewModel
{
    public IncentiveRegisterFilter Filter { get; set; } = new();
    public IReadOnlyList<string> PartyCodes { get; set; } = [];
    public IReadOnlyList<PartyLookupItem> Parties { get; set; } = [];
    public IReadOnlyList<(int Month, int Year, string Label)> Periods { get; set; } = [];
    public IReadOnlyList<(int Month, int Year, string Label)> PaymentPeriods { get; set; } = [];
    public IReadOnlyList<IncentiveRegisterRow> Rows { get; set; } = [];
    public IReadOnlyList<IncentivePortal.Models.ReportColumnConfig> ColumnConfigs { get; set; } = [];
}

public sealed record PartyLookupItem(int Id, string PartyCode, string PartyName, string OriginalPartyCode = "");

public sealed record IncentiveRegisterRow(
    string MonthLabel,
    int Month,
    int Year,
    string PartyCode,
    string PartyName,
    decimal SaleValue,
    decimal SlabPercent,
    decimal OnBillDiscount,
    decimal AchievementPercent,
    decimal GrossIncentive,
    decimal TdsAmount,
    decimal? NetTransferAmount,
    decimal TransferredAmount,
    DateTime? ProcessingDate,
    DateTime? PaymentDate,
    string PaymentStatus,
    string? UTRNumber,
    string BankAccountNumber,
    string IFSC,
    string BeneficiaryName,
    string BranchCode = "-",
    string OriginalPartyCode = "-",
    string SalesExecutive = "-");

public sealed record BranchRankingDto(
    string BranchName,
    string Code,
    decimal TotalSales,
    decimal IncentiveGenerated,
    decimal GrowthPercent);

public sealed record DealerHistoryDto(
    string MonthName,
    int Month,
    int Year,
    decimal Sales,
    decimal Incentive, // Maps to Gross Incentive for chart continuity
    decimal SlabPercent,
    decimal TdsAmount,
    decimal NetCreditedAmount,
    string Status,
    string CreditedOn,
    string Utr);

public sealed record DashboardChartDataDto(
    IReadOnlyList<decimal> SalesTrend,
    IReadOnlyList<decimal> IncentiveTrend,
    IReadOnlyList<string> Months,
    IReadOnlyList<string> BranchNames,
    IReadOnlyList<decimal> BranchSales,
    IReadOnlyList<string> SlabRanges,
    IReadOnlyList<int> SlabCounts,
    IReadOnlyList<string> LocationLabels,
    IReadOnlyList<decimal> LocationSales,
    IReadOnlyList<string> CategoryLabels,
    IReadOnlyList<decimal> CategorySales);

public sealed record SmartInsightDto(
    string Category,
    string Message,
    string Severity);

public sealed record IncentiveSchemeSlabDto(
    decimal MinAchievement,
    decimal MaxAchievement,
    decimal Percentage,
    decimal FixedAmount,
    string RuleName);

public sealed record OutstandingMasterRow(
    string PartyCode,
    string PartyName,
    string BranchCode,
    string BranchName,
    int Year,
    int Month,
    decimal SaleValue,
    decimal Outstanding,
    DateTime LastSyncedAt,
    decimal? OutstandingLess7Days = null,
    decimal? Outstanding7To14Days = null,
    decimal? Outstanding14To21Days = null,
    decimal? Outstanding21To28Days = null,
    decimal? Outstanding28To35Days = null,
    decimal? Outstanding35To50Days = null,
    decimal? Outstanding50To80Days = null,
    decimal? OutstandingMore80Days = null);

public sealed record OutstandingAdjustmentRow(
    string PartyCode,
    string PartyName,
    decimal GrossIncentive,
    decimal TdsAmount,
    decimal NetTransferAmount,
    decimal Outstanding);



