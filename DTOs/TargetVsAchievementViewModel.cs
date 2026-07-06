using System.Collections.Generic;

namespace IncentivePortal.DTOs;

public sealed class TargetVsAchievementViewModel
{
    public int SelectedMonth { get; set; }
    public int SelectedYear { get; set; }
    public string? SelectedBranch { get; set; }
    public List<string> AvailableBranches { get; set; } = new();
    public string? SelectedPartyType { get; set; }
    public List<string> AvailablePartyTypes { get; set; } = new();
    public string? SelectedPartCategory { get; set; }
    public List<string> AvailablePartCategories { get; set; } = new();
    public List<TargetVsAchievementRow> Rows { get; set; } = new();
}

public sealed class TargetVsAchievementRow
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public decimal SystemSuggestedTarget { get; set; }
    public decimal? AdminDefinedTarget { get; set; }
    public decimal FinalTarget { get; set; }
    public decimal CurrentAchievementSales { get; set; }
    public decimal AchievementPercent { get; set; }
    public string CurrentSlabLabel { get; set; } = "None";
    public decimal NextSlabTarget { get; set; }
    public decimal LastMonthSales { get; set; }
    public decimal YTDSales { get; set; }
    public decimal LastYearYTDSales { get; set; }
    public decimal YoYGrowthPercent { get; set; }
}
