using System.Collections.Generic;

namespace IncentivePortal.DTOs;

public class Customer360ViewModel
{
    // Search Mode
    public bool IsSearchMode { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public List<DealerSearchDto> SearchResults { get; set; } = new();

    // Party Overview
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string GST { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string DealerType { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    public string ExecutiveName { get; set; } = "Unassigned";

    // KPI Cards
    public decimal CurrentMonthSales { get; set; }
    public decimal CurrentMonthTarget { get; set; }
    public decimal CurrentOutstanding { get; set; }
    public decimal YTDSales { get; set; }
    
    // Performance & Slabs
    public decimal CurrentSlabPercent { get; set; }
    public decimal NextSlabPercent { get; set; }
    public decimal NextSlabTarget { get; set; }
    public decimal ProgressToNextSlabPercent { get; set; }
    
    // Trend Data (for Chart)
    public List<string> TrendLabels { get; set; } = new();
    public List<decimal> SalesTrend { get; set; } = new();
    public List<decimal> IncentiveTrend { get; set; } = new();

    // Bank Details
    public string BankAccountHolder { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankIFSC { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankApprovalStatus { get; set; } = string.Empty;
    
    // Recent Transactions (Raw Data)
    // Recent Transactions (Raw Data)
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();

    // Summary Analytics
    public List<SalesSummaryDto> MonthWiseSales { get; set; } = new();
    public List<SalesSummaryDto> CategoryWiseSales { get; set; } = new();

    // YoY Matrix
    public List<int> AvailableYears { get; set; } = new();
    public List<YearMonthSalesDto> MonthYearMatrix { get; set; } = new();
}

public class YearMonthSalesDto
{
    public string MonthName { get; set; } = string.Empty;
    public int MonthNumber { get; set; }
    public Dictionary<int, YearlySalesData> YearlyData { get; set; } = new();
}

public class YearlySalesData
{
    public decimal Sales { get; set; }
    public decimal? GrowthPercentage { get; set; }
}

public class SalesSummaryDto
{
    public string Label { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalQty { get; set; }
}

public class DealerSearchDto
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
}

public class RecentTransactionDto
{
    public string Date { get; set; } = string.Empty;
    public string DocumentNum { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal NetSales { get; set; }
}
