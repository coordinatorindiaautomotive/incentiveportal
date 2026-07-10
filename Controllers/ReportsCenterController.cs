using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class ReportsCenterController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var colConfigs = await db.ReportColumnConfigs
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.ReportName).ThenBy(c => c.SortOrder)
            .ToListAsync();

        // Seed default report column configs if none exist to support custom reports
        if (!colConfigs.Any())
        {
            var seedConfigs = new[]
            {
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "PartyCode", DisplayName = "Dealer Code", IsVisible = true, SortOrder = 1 },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "PartyName", DisplayName = "Dealer Name", IsVisible = true, SortOrder = 2 },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "SaleValue", DisplayName = "Net Sales (₹)", IsVisible = true, SortOrder = 3, Format = "N2" },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "SlabPercent", DisplayName = "Slab (%)", IsVisible = true, SortOrder = 4, Format = "P2" },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "GrossIncentive", DisplayName = "Gross Incentive (₹)", IsVisible = true, SortOrder = 5, Format = "N2" },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "TdsAmount", DisplayName = "TDS Deducted (₹)", IsVisible = true, SortOrder = 6, Format = "N2" },
                new ReportColumnConfig { ReportName = "IncentiveRegister", ColumnKey = "NetTransferAmount", DisplayName = "Net Payout (₹)", IsVisible = true, SortOrder = 7, Format = "N2" }
            };

            db.ReportColumnConfigs.AddRange(seedConfigs);
            await db.SaveChangesAsync();
            colConfigs = seedConfigs.ToList();
        }

        // Mock saved filters list
        var savedFilters = new[]
        {
            new { Id = 1, Name = "High Performing Dealers", Report = "Incentive Register", QueryString = "?minSales=500000" },
            new { Id = 2, Name = "Pending Bank Approvals", Report = "Approvals", QueryString = "?status=Pending" },
            new { Id = 3, Name = "Critical Cash Mismatch", Report = "Daily closing Cash", QueryString = "?hasVariance=true" }
        };

        ViewBag.ColumnConfigs = colConfigs;
        ViewBag.SavedFilters = savedFilters;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveColumnConfig(string reportName, string columnKey, string displayName, bool isVisible, int sortOrder)
    {
        if (string.IsNullOrEmpty(reportName) || string.IsNullOrEmpty(columnKey))
            return BadRequest("Report Name and Column Key are required.");

        var existing = await db.ReportColumnConfigs
            .FirstOrDefaultAsync(c => c.ReportName == reportName && c.ColumnKey == columnKey && !c.IsDeleted);

        if (existing != null)
        {
            existing.DisplayName = displayName;
            existing.IsVisible = isVisible;
            existing.SortOrder = sortOrder;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }
        else
        {
            var config = new ReportColumnConfig
            {
                ReportName = reportName,
                ColumnKey = columnKey,
                DisplayName = displayName,
                IsVisible = isVisible,
                SortOrder = sortOrder,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = User.Identity?.Name ?? "system"
            };
            db.ReportColumnConfigs.Add(config);
        }
        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Report column configuration saved." });
    }

    [HttpGet("ReportsCenter/DynamicBuilder")]
    public IActionResult DynamicBuilder()
    {
        return View();
    }
}
