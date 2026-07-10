using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using IncentivePortal.Application.Reports;
using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IncentivePortal.Controllers;

[Authorize]
[ApiController]
[Route("DynamicReport")]
public sealed class DynamicReportApiController(
    IDynamicReportService dynamicReportService,
    IncentiveDbContext db
) : ControllerBase
{
    [HttpPost("GetData")]
    public async Task<IActionResult> GetData([FromBody] QueryDataRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var data = await dynamicReportService.QueryReportDataAsync(
                request.ReportType,
                request.Dimensions,
                request.Values,
                request.Filters,
                request.Sorts,
                request.Page,
                request.PageSize,
                cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
    [HttpGet("GetFilterMetadata")]
    public async Task<IActionResult> GetFilterMetadata(CancellationToken cancellationToken)
    {
        try
        {
            var years = await db.Raws
                .Where(x => !x.IsDeleted && x.YearNumber.HasValue)
                .Select(x => x.YearNumber!.Value)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            var months = await db.Raws
                .Where(x => !x.IsDeleted && x.Month != null)
                .Select(x => x.Month)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            var categories = await db.Raws
                .Where(x => !x.IsDeleted && x.PartCategoryCode != null)
                .Select(x => x.PartCategoryCode)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(cancellationToken);

            return Ok(new { years, months, categories });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("GetLayouts")]
    public async Task<IActionResult> GetLayouts(CancellationToken cancellationToken)
    {
        try
        {
            var layouts = await db.CustomReportLayouts
                .Where(x => !x.IsDeleted)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);
            return Ok(layouts);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("SaveLayout")]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "Report name is required." });

            var layout = await db.CustomReportLayouts
                .FirstOrDefaultAsync(x => x.Name == request.Name && !x.IsDeleted, cancellationToken);

            if (layout == null)
            {
                layout = new CustomReportLayout
                {
                    Name = request.Name,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system"
                };
                db.CustomReportLayouts.Add(layout);
            }
            else
            {
                layout.UpdatedAt = DateTime.UtcNow;
                layout.UpdatedBy = User.Identity?.Name ?? "system";
            }

            layout.ReportType = request.ReportType;
            layout.SelectedFieldsJson = request.SelectedFieldsJson;
            layout.PivotRowsJson = request.PivotRowsJson;
            layout.PivotColumnsJson = request.PivotColumnsJson;
            layout.PivotValuesJson = request.PivotValuesJson;
            layout.FiltersJson = request.FiltersJson;
            layout.SortsJson = request.SortsJson;
            layout.GroupsJson = request.GroupsJson;

            await db.SaveChangesAsync(cancellationToken);
            return Ok(new { ok = true, message = "Report layout saved successfully.", id = layout.Id });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("Export")]
    public async Task<IActionResult> Export([FromBody] QueryDataRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await dynamicReportService.ExportReportDataAsync(
                "csv",
                request.ReportType,
                request.Dimensions,
                request.Values,
                request.Filters,
                request.Sorts,
                cancellationToken);

            var filename = $"Dynamic_Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv", filename);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("SaveSchedule")]
    public async Task<IActionResult> SaveSchedule([FromBody] SaveScheduleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RecipientEmails))
                return BadRequest(new { message = "Recipient emails are required." });

            var layout = await db.CustomReportLayouts
                .FirstOrDefaultAsync(x => x.Id == request.LayoutId && !x.IsDeleted, cancellationToken);

            if (layout == null)
                return NotFound(new { message = "Report layout not found." });

            var schedule = await db.ReportSchedules
                .FirstOrDefaultAsync(x => x.LayoutId == request.LayoutId && !x.IsDeleted, cancellationToken);

            if (schedule == null)
            {
                schedule = new ReportSchedule
                {
                    LayoutId = request.LayoutId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = User.Identity?.Name ?? "system"
                };
                db.ReportSchedules.Add(schedule);
            }
            else
            {
                schedule.UpdatedAt = DateTime.UtcNow;
                schedule.UpdatedBy = User.Identity?.Name ?? "system";
            }

            schedule.RecipientEmails = request.RecipientEmails;
            schedule.Frequency = request.Frequency;
            schedule.IsActive = request.IsActive;

            // Generate clean cron expression
            schedule.CronExpression = request.Frequency switch
            {
                "Daily" => "0 8 * * *",       // Daily at 8:00 AM
                "Weekly" => "0 8 * * 1",      // Weekly on Monday at 8:00 AM
                "Monthly" => "0 8 1 * *",     // Monthly on the 1st at 8:00 AM
                _ => "0 8 * * *"
            };

            await db.SaveChangesAsync(cancellationToken);

            // Register/Update Hangfire Recurring Job
            var jobId = $"dynamic-report-schedule-{schedule.Id}";
            if (schedule.IsActive)
            {
                RecurringJob.AddOrUpdate<DynamicReportSchedulerJob>(
                    jobId,
                    job => job.RunScheduleAsync(schedule.Id),
                    schedule.CronExpression);
                schedule.LastRunJobId = jobId;
            }
            else
            {
                RecurringJob.RemoveIfExists(jobId);
            }

            await db.SaveChangesAsync(cancellationToken);

            return Ok(new { ok = true, message = "Report schedule configured successfully." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public sealed class QueryDataRequest
{
    public string ReportType { get; set; } = "Tabular";
    public List<string> Dimensions { get; set; } = new();
    public List<PivotValueMetric> Values { get; set; } = new();
    public List<FilterCriterion> Filters { get; set; } = new();
    public List<SortCriterion> Sorts { get; set; } = new();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class SaveLayoutRequest
{
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = "Tabular";
    public string SelectedFieldsJson { get; set; } = "[]";
    public string PivotRowsJson { get; set; } = "[]";
    public string PivotColumnsJson { get; set; } = "[]";
    public string PivotValuesJson { get; set; } = "[]";
    public string FiltersJson { get; set; } = "[]";
    public string SortsJson { get; set; } = "[]";
    public string GroupsJson { get; set; } = "[]";
}

public sealed class SaveScheduleRequest
{
    public int LayoutId { get; set; }
    public string RecipientEmails { get; set; } = string.Empty;
    public string Frequency { get; set; } = "Daily";
    public bool IsActive { get; set; } = true;
}
