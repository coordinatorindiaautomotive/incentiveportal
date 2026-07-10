using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IncentivePortal.Data;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Application.Reports;

public sealed class DynamicReportSchedulerJob(
    IncentiveDbContext db,
    IDynamicReportService dynamicReportService,
    INotificationService notificationService
)
{
    public async Task RunScheduleAsync(int scheduleId)
    {
        var schedule = await db.ReportSchedules
            .Include(x => x.Layout)
            .FirstOrDefaultAsync(x => x.Id == scheduleId && !x.IsDeleted && x.IsActive);

        if (schedule == null || schedule.Layout == null) return;

        var dims = JsonSerializer.Deserialize<List<string>>(schedule.Layout.SelectedFieldsJson) ?? new();
        var vals = JsonSerializer.Deserialize<List<PivotValueMetric>>(schedule.Layout.PivotValuesJson) ?? new();
        var filts = JsonSerializer.Deserialize<List<FilterCriterion>>(schedule.Layout.FiltersJson) ?? new();
        var sorts = JsonSerializer.Deserialize<List<SortCriterion>>(schedule.Layout.SortsJson) ?? new();

        var resultObj = await dynamicReportService.QueryReportDataAsync(
            schedule.Layout.ReportType,
            dims,
            vals,
            filts,
            sorts,
            1,
            200,
            default);

        var dynamicResult = (dynamic)resultObj;
        var dataList = (List<Dictionary<string, object>>)dynamicResult.data;
        var totalCount = (int)dynamicResult.totalCount;

        var sb = new StringBuilder();
        sb.Append("<div style='font-family: Arial, sans-serif; color: #333;'>");
        sb.Append($"<h2>ThessBuddy Dynamic Report: {schedule.Layout.Name}</h2>");
        sb.Append($"<p>This is a scheduled delivery for the custom report layout <strong>{schedule.Layout.Name}</strong> ({schedule.Layout.ReportType}).</p>");
        
        if (dataList.Count > 0)
        {
            sb.Append("<table border='1' cellpadding='6' cellspacing='0' style='border-collapse: collapse; border-color: #ddd; width: 100%; text-align: left;'>");
            
            sb.Append("<tr style='background-color: #f2f2f2;'>");
            var headers = dataList[0].Keys.ToList();
            foreach (var h in headers)
            {
                sb.Append($"<th>{h}</th>");
            }
            sb.Append("</tr>");

            foreach (var row in dataList)
            {
                sb.Append("<tr>");
                foreach (var h in headers)
                {
                    var val = row.TryGetValue(h, out var v) ? v : null;
                    sb.Append($"<td>{val ?? "—"}</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
            if (totalCount > 200)
            {
                sb.Append($"<p><em>* Only the first 200 of {totalCount} records are shown in this email. Log in to the portal to view full details.</em></p>");
            }
        }
        else
        {
            sb.Append("<p>No matching records were found for this scheduled report period.</p>");
        }

        sb.Append("<hr/><p style='font-size: 11px; color: #777;'>Sent automatically by ThessBuddy Distribution Platform.</p>");
        sb.Append("</div>");

        var recipients = schedule.RecipientEmails.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var email in recipients)
        {
            await notificationService.SendEmailAsync(
                email.Trim(),
                $"ThessBuddy Schedule: {schedule.Layout.Name}",
                sb.ToString(),
                default);
        }
    }
}
