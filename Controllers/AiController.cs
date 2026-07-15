using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.Helpers;

namespace IncentivePortal.Controllers;

[ApiController]
[Route("api/[controller]")]
[ApiKeyAuth]
public sealed class AiController(IncentiveDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns a flat list of all active Branches in the system.
    /// Optimized for low token count LLM context injection.
    /// </summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches(CancellationToken cancellationToken)
    {
        var branches = await db.Branches
            .AsNoTracking()
            .Where(b => !b.IsDeleted)
            .OrderBy(b => b.Name)
            .Select(b => new {
                code = b.Code,
                name = b.Name,
                region = b.Region
            })
            .ToListAsync(cancellationToken);

        return Ok(branches);
    }

    /// <summary>
    /// Returns a highly condensed summary of current platform state.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        int currentMonth = now.Month;
        int currentYear = now.Year;

        var totals = await db.SsIncentives
            .AsNoTracking()
            .Where(i => i.Month == currentMonth && i.Year == currentYear && !i.IsDeleted)
            .GroupBy(i => 1)
            .Select(g => new {
                TotalGross = g.Sum(i => i.GrossIncentive),
                TotalTds = g.Sum(i => i.TdsAmount),
                TotalNet = g.Sum(i => i.NetTransferAmount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var activeUploadTemplatesCount = await db.ImportTemplates
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var outstandingCount = await db.DealerOutstandings
            .AsNoTracking()
            .Where(o => o.Month == currentMonth && o.Year == currentYear && !o.IsDeleted && o.Outstanding > 0)
            .CountAsync(cancellationToken);

        return Ok(new
        {
            Period = $"{currentMonth:D2}/{currentYear}",
            Summary = new
            {
                TotalGrossIncentives = totals?.TotalGross ?? 0,
                TotalTdsDeducted = totals?.TotalTds ?? 0,
                TotalNetPayouts = totals?.TotalNet ?? 0,
                TotalDealersWithOutstanding = outstandingCount,
                ActiveUploadTemplates = activeUploadTemplatesCount
            }
        });
    }

    /// <summary>
    /// Returns a text-based definition of the requested Excel Upload Template schema 
    /// so an LLM can understand how to dynamically construct Excel files for the platform.
    /// </summary>
    [HttpGet("schema")]
    public async Task<IActionResult> GetSchema([FromQuery] string templateCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templateCode))
        {
            return BadRequest(new { message = "?templateCode is required" });
        }

        var template = await db.ImportTemplates
            .AsNoTracking()
            .Include(t => t.Mappings)
            .Include(t => t.ValidationRules)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Code == templateCode, cancellationToken);

        if (template == null)
        {
            return NotFound(new { message = $"Template '{templateCode}' not found." });
        }

        var columns = template.Mappings.OrderBy(m => m.Id).Select(m => new {
            Header = m.SourceHeader,
            TargetTable = template.TargetTable,
            DestinationColumn = m.DestinationColumn,
            Required = true // Hardcoded for simplified schema context
        });

        var rules = template.ValidationRules.Select(r => new {
            Type = r.ValidationType,
            Target = r.ColumnName,
            Expression = r.ValidationConfig
        });

        return Ok(new
        {
            TemplateName = template.Name,
            Entity = template.TargetTable,
            Columns = columns,
            ValidationRules = rules
        });
    }
}
