using System;
using System.Collections.Generic;
using IncentivePortal.DTOs;

namespace IncentivePortal.Controllers;

public class PerformanceReportsViewModel
{
    public List<YearSummaryItem> YearSummary { get; set; } = new();
    public List<MonthSummaryItem> MonthSummary { get; set; } = new();
    public List<EmployeeHistoryItem> EmployeeHistory { get; set; } = new();
    public List<DealerHistoryItem> DealerHistory { get; set; } = new();
    public List<SlabPerformanceItem> SlabPerformance { get; set; } = new();
    public List<StatusSummaryItem> StatusSummary { get; set; } = new();
    public string RawSalesJson { get; set; } = string.Empty;
    public string RawSalesJsonLastYear { get; set; } = string.Empty;
    public string DealerSalesJson { get; set; } = string.Empty;

    public List<string> SelectedDealerTypes { get; set; } = new();
    public List<int> SelectedBranchIds { get; set; } = new();
    public List<string> SelectedPartyCodes { get; set; } = new();
    public List<string> SelectedCategories { get; set; } = new();
    public int SelectedMonth { get; set; }
    public int SelectedYear { get; set; }

    public int AnchorMonth { get; set; }
    public int AnchorYear { get; set; }
    public decimal CmSales { get; set; }
    public decimal CmGross { get; set; }
    public decimal CmNet { get; set; }
    public int CmCount { get; set; }

    public decimal? SalesMoM { get; set; }
    public decimal? IncentiveMoM { get; set; }
    public decimal? SalesYoY { get; set; }
    public decimal? IncentiveYoY { get; set; }

    public List<string> AvailableDealerTypes { get; set; } = new();
    public List<BranchLookupItem> AvailableBranches { get; set; } = new();
    public List<PartyLookupItem> AvailableParties { get; set; } = new();
    public List<string> AvailableCategories { get; set; } = new();
    public List<(int Month, int Year, string Label)> AvailablePeriods { get; set; } = new();
}

public class YearSummaryItem
{
    public int Year { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public int RecordCount { get; set; }
}

public class MonthSummaryItem
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal TotalSales { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public int RecordCount { get; set; }
}

public class EmployeeHistoryItem
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public int DealersServed { get; set; }
}

public class DealerHistoryItem
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
}

public class SlabPerformanceItem
{
    public string Slab { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalGross { get; set; }
    public decimal TotalNet { get; set; }
    public int Count { get; set; }
}

public class StatusSummaryItem
{
    public string Status { get; set; } = string.Empty;
    public decimal TotalNet { get; set; }
    public int Count { get; set; }
}

public record BranchLookupItem(int Id, string Code, string Name, string Consignee);
