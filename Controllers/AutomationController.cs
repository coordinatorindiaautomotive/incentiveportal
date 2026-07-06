using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class AutomationController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var rules = await db.AutomationRules
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        // Seed some default automation rules if none exist to make the dashboard look rich and ready
        if (!rules.Any())
        {
            var seedRules = new List<AutomationRule>
            {
                new() { RuleName = "Outstanding Exceeded Warning", TriggerType = "OutstandingLimit", ActionType = "Notification", ConditionsJson = "{\"limit\": 500000}", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "system" },
                new() { RuleName = "Daily Closing Mismatch Alert", TriggerType = "ClosingMismatch", ActionType = "Email", ConditionsJson = "{\"tolerance\": 0}", IsActive = true, CreatedAt = DateTime.UtcNow, CreatedBy = "system" },
                new() { RuleName = "SLA Expiration Warning", TriggerType = "SlaWarning", ActionType = "Email", ConditionsJson = "{\"hoursBefore\": 4}", IsActive = false, CreatedAt = DateTime.UtcNow, CreatedBy = "system" }
            };

            db.AutomationRules.AddRange(seedRules);
            await db.SaveChangesAsync();
            rules = seedRules;
        }

        // Mock recent execution history
        var history = new[]
        {
            new { Time = "Today, 10:30 AM", Rule = "Outstanding Exceeded Warning", Event = "Rama Motors outstanding reached ₹620,000", Status = "Triggered", Action = "In-App Notification Sent" },
            new { Time = "Yesterday, 06:15 PM", Rule = "Daily Closing Mismatch Alert", Event = "North Branch reported ₹250 cash variance", Status = "Triggered", Action = "Email sent to HO Auditor" },
            new { Time = "28 Jun 2026", Rule = "Outstanding Exceeded Warning", Event = "Nitin Auto Parts outstanding reached ₹510,000", Status = "Triggered", Action = "In-App Notification Sent" }
        };

        ViewBag.History = history;
        return View(rules);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AutomationRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.RuleName))
            return BadRequest("Rule name is required.");

        rule.ConditionsJson = rule.ConditionsJson ?? "{}";

        if (rule.Id == 0)
        {
            rule.CreatedAt = DateTime.UtcNow;
            rule.CreatedBy = User.Identity?.Name ?? "system";
            rule.IsActive = true;
            db.AutomationRules.Add(rule);
        }
        else
        {
            var existing = await db.AutomationRules.FindAsync(rule.Id);
            if (existing == null || existing.IsDeleted)
                return NotFound("Rule not found.");

            existing.RuleName = rule.RuleName;
            existing.TriggerType = rule.TriggerType;
            existing.ActionType = rule.ActionType;
            existing.ConditionsJson = rule.ConditionsJson;
            existing.IsActive = rule.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Automation rule saved successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var existing = await db.AutomationRules.FindAsync(id);
        if (existing == null || existing.IsDeleted)
            return NotFound("Rule not found.");

        existing.IsActive = !existing.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = $"Rule status toggled.", isActive = existing.IsActive });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await db.AutomationRules.FindAsync(id);
        if (existing == null) return NotFound("Rule not found.");

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Automation rule deleted." });
    }
}
