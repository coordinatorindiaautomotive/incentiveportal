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
public sealed class TasksController(IncentiveDbContext db) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetTasks(string partyCode)
    {
        var tasks = await db.CustomerTasks
            .Where(t => t.PartyCode == partyCode && !t.IsDeleted)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new {
                t.Id,
                t.Title,
                t.Description,
                t.Status,
                t.Priority,
                dueDate = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : "",
                t.AssignedTo,
                t.TaskType,
                createdAt = t.CreatedAt.ToString("dd MMM yyyy")
            })
            .ToListAsync();

        return Json(tasks);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CustomerTask task)
    {
        if (string.IsNullOrWhiteSpace(task.Title))
            return BadRequest("Task title is required.");
        if (string.IsNullOrWhiteSpace(task.PartyCode))
            return BadRequest("Customer selection is required.");

        task.Description = task.Description ?? string.Empty;
        task.AssignedTo = task.AssignedTo ?? string.Empty;

        if (task.Id == 0)
        {
            task.CreatedAt = DateTime.UtcNow;
            task.CreatedBy = User.Identity?.Name ?? "system";
            task.Status = "Pending";
            db.CustomerTasks.Add(task);
        }
        else
        {
            var existing = await db.CustomerTasks.FindAsync(task.Id);
            if (existing == null || existing.IsDeleted)
                return NotFound("Task not found.");

            existing.Title = task.Title;
            existing.Description = task.Description;
            existing.Status = task.Status;
            existing.Priority = task.Priority;
            existing.DueDate = task.DueDate;
            existing.AssignedTo = task.AssignedTo;
            existing.TaskType = task.TaskType;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Task saved successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var existing = await db.CustomerTasks.FindAsync(id);
        if (existing == null || existing.IsDeleted)
            return NotFound("Task not found.");

        existing.Status = existing.Status == "Completed" ? "Pending" : "Completed";
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = $"Task status changed to {existing.Status}.", status = existing.Status });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var existing = await db.CustomerTasks.FindAsync(id);
        if (existing == null) return NotFound("Task not found.");

        existing.IsDeleted = true;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Task deleted successfully." });
    }
}
