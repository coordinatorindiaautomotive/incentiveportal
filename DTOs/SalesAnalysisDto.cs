using System.Collections.Generic;

namespace IncentivePortal.DTOs;

public class SalesAnalysisDto
{
    public string MonthYear { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PartNum { get; set; } = string.Empty;
    public decimal TotalSales { get; set; }
    public decimal TotalQty { get; set; }
}

public class DataTableResponse<T>
{
    public int draw { get; set; }
    public int recordsTotal { get; set; }
    public int recordsFiltered { get; set; }
    public List<T> data { get; set; } = new();
}
