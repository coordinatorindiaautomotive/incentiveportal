using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class HelpdeskController(IncentiveDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? status, string? priority, string? search)
    {
        var query = db.HelpdeskTickets.Where(t => !t.IsDeleted);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(t => t.Status == status);
        if (!string.IsNullOrEmpty(priority))
            query = query.Where(t => t.Priority == priority);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(t => t.Title.Contains(search) || t.Description.Contains(search) || (t.AssociatedPartyCode != null && t.AssociatedPartyCode.Contains(search)));

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        ViewBag.StatusFilter = status;
        ViewBag.PriorityFilter = priority;
        ViewBag.SearchFilter = search;

        // Fetch dealers for the Ticket creation dropdown
        ViewBag.Parties = await db.Parties
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.PartyName)
            .Select(p => new { p.PartyCode, p.PartyName })
            .ToListAsync();

        // Fetch users for assignments
        ViewBag.Users = await db.Users
            .Where(u => !u.IsDeleted)
            .OrderBy(u => u.UserName)
            .Select(u => u.UserName)
            .ToListAsync();

        // Count totals
        ViewBag.TotalCount = await db.HelpdeskTickets.CountAsync(t => !t.IsDeleted);
        ViewBag.NewCount = await db.HelpdeskTickets.CountAsync(t => t.Status == "New" && !t.IsDeleted);
        ViewBag.InprogressCount = await db.HelpdeskTickets.CountAsync(t => (t.Status == "InProgress" || t.Status == "Assigned") && !t.IsDeleted);
        ViewBag.ResolvedCount = await db.HelpdeskTickets.CountAsync(t => (t.Status == "Resolved" || t.Status == "Closed") && !t.IsDeleted);

        // Prepopulate Knowledge Base Articles
        ViewBag.KBArticles = await db.KnowledgeBaseArticles
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.ViewsCount)
            .Take(5)
            .ToListAsync();

        return View(tickets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(HelpdeskTicket ticket)
    {
        ticket.Remarks = ticket.Remarks ?? string.Empty;
        ticket.AssociatedPartyCode = ticket.AssociatedPartyCode ?? string.Empty;
        ticket.AssignedTo = ticket.AssignedTo ?? string.Empty;
        ticket.AttachmentPath = ticket.AttachmentPath ?? string.Empty;

        if (ticket.Id == 0)
        {
            ticket.CreatedAt = DateTime.UtcNow;
            ticket.CreatedBy = User.Identity?.Name ?? "system";
            ticket.Status = string.IsNullOrEmpty(ticket.AssignedTo) ? "New" : "Assigned";
            ticket.SlaExpiry = DateTime.UtcNow.AddHours(48); // Standard SLA is 48 hours
            db.HelpdeskTickets.Add(ticket);
        }
        else
        {
            var existing = await db.HelpdeskTickets.FindAsync(ticket.Id);
            if (existing == null || existing.IsDeleted)
                return NotFound("Ticket not found.");

            existing.Title = ticket.Title;
            existing.Description = ticket.Description;
            existing.Priority = ticket.Priority;
            existing.Status = ticket.Status;
            existing.AssignedTo = ticket.AssignedTo;
            existing.Category = ticket.Category;
            existing.Remarks = ticket.Remarks;
            existing.AssociatedPartyCode = ticket.AssociatedPartyCode;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Helpdesk Ticket saved successfully." });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await db.HelpdeskTickets
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted);

        if (ticket == null)
            return NotFound("Ticket not found.");

        var comments = ticket.Comments
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new {
                c.CommentText,
                c.IsInternal,
                createdAt = c.CreatedAt.ToString("dd MMM yyyy HH:mm"),
                createdBy = c.CreatedBy
            }).ToList();

        return Json(new {
            ticket.Id,
            ticket.Title,
            ticket.Description,
            ticket.Priority,
            ticket.Status,
            ticket.AssignedTo,
            ticket.Category,
            ticket.Remarks,
            ticket.AssociatedPartyCode,
            slaExpiry = ticket.SlaExpiry?.ToString("yyyy-MM-dd HH:mm"),
            comments
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int ticketId, string commentText, bool isInternal)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return BadRequest("Comment text cannot be empty.");

        var ticket = await db.HelpdeskTickets.FindAsync(ticketId);
        if (ticket == null || ticket.IsDeleted)
            return NotFound("Ticket not found.");

        var comment = new TicketComment
        {
            TicketId = ticketId,
            CommentText = commentText,
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };

        db.TicketComments.Add(comment);

        // Auto-progress ticket status on first comment if it was 'New' or 'Assigned'
        if (ticket.Status == "New" || ticket.Status == "Assigned")
        {
            ticket.Status = "InProgress";
            ticket.UpdatedAt = DateTime.UtcNow;
            ticket.UpdatedBy = User.Identity?.Name ?? "system";
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Comment added successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var ticket = await db.HelpdeskTickets.FindAsync(id);
        if (ticket == null) return NotFound("Ticket not found.");

        ticket.IsDeleted = true;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "Ticket deleted successfully." });
    }
}
