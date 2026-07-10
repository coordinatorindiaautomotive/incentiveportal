using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Application.Reports;

public interface IDynamicReportService
{
    Task<object> QueryReportDataAsync(
        string reportType, 
        List<string> dimensions, 
        List<PivotValueMetric> values, 
        List<FilterCriterion> filters, 
        List<SortCriterion> sorts, 
        int page, 
        int pageSize, 
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportReportDataAsync(
        string format, 
        string reportType, 
        List<string> dimensions, 
        List<PivotValueMetric> values, 
        List<FilterCriterion> filters, 
        List<SortCriterion> sorts, 
        CancellationToken cancellationToken = default);
}

public sealed class PivotValueMetric
{
    public string Field { get; set; } = string.Empty;
    public string Aggregation { get; set; } = "Sum"; // Sum, Count, Avg, Min, Max
}

public sealed class FilterCriterion
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "in"; // in, notin, contains, range
    public List<string> Values { get; set; } = new();
}

public sealed class SortCriterion
{
    public string Field { get; set; } = string.Empty;
    public bool Descending { get; set; }
}
