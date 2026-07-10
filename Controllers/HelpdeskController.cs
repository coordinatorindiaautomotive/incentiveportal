using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class HelpdeskController(
    IncentiveDbContext db,
    ICurrentUser currentUser
) : Controller
{
    private bool IsHO() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance) ||
        currentUser.IsInRole(AppRoles.Auditor);

    private bool IsEngineer() =>
        currentUser.IsInRole(AppRoles.SuperAdmin) ||
        currentUser.IsInRole(AppRoles.HOFinance) ||
        currentUser.IsInRole(AppRoles.BranchManager);

    private IQueryable<ItTicket> ScopedTickets() =>
        IsHO()
            ? db.ItTickets.Include(x => x.Branch)
            : db.ItTickets.Include(x => x.Branch)
                           .Where(x => x.BranchId == currentUser.BranchId || x.Requester == currentUser.UserName);

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> GetMasterOptions(CancellationToken ct)
    {
        var allMasters = await db.ItMasterDatas
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Type, x.Name, x.Code })
            .ToListAsync(ct);

        return Json(new {
            categories = allMasters.Where(x => x.Type == "TicketCategory").ToList(),
            subCategories = allMasters.Where(x => x.Type == "TicketSubCategory").ToList(),
            priorities = allMasters.Where(x => x.Type == "Priority").ToList(),
            severities = allMasters.Where(x => x.Type == "Severity").ToList(),
            impacts = allMasters.Where(x => x.Type == "Impact").ToList(),
            rootCauses = allMasters.Where(x => x.Type == "RootCause").ToList(),
            resolutionTypes = allMasters.Where(x => x.Type == "ResolutionType").ToList(),
            departments = allMasters.Where(x => x.Type == "Department").ToList(),
            statuses = allMasters.Where(x => x.Type == "Status").ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetTickets(
        string? status, int? priorityId, int? categoryId, string? search,
        int page = 1, int pageSize = 15, CancellationToken ct = default)
    {
        var q = ScopedTickets();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
        if (priorityId.HasValue && priorityId > 0) q = q.Where(x => x.PriorityId == priorityId);
        if (categoryId.HasValue && categoryId > 0) q = q.Where(x => x.CategoryId == categoryId);
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            q = q.Where(x => x.Subject.Contains(search) || x.TicketNumber.Contains(search) ||
                              x.Description.Contains(search) || x.Requester.Contains(search));
        }

        var total = await q.CountAsync(ct);
        var rawItems = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct);

        // Map lookup details
        var masterIds = rawItems.SelectMany(x => new[] {
            x.DepartmentId, x.CategoryId, x.SubCategoryId, x.PriorityId, x.SeverityId, x.ImpactId
        }).Distinct().ToList();

        var masterMap = await db.ItMasterDatas
            .Where(x => masterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var items = rawItems.Select(x => new {
            x.Id, x.TicketNumber, x.Requester, x.Subject, x.Status, x.AssignedEngineer,
            department = masterMap.TryGetValue(x.DepartmentId, out var dept) ? dept : "Other",
            category = masterMap.TryGetValue(x.CategoryId, out var cat) ? cat : "Other",
            subCategory = masterMap.TryGetValue(x.SubCategoryId, out var sub) ? sub : "Other",
            priority = masterMap.TryGetValue(x.PriorityId, out var pri) ? pri : "Medium",
            severity = masterMap.TryGetValue(x.SeverityId, out var sev) ? sev : "Medium",
            impact = masterMap.TryGetValue(x.ImpactId, out var imp) ? imp : "Low",
            x.DepartmentId, x.CategoryId, x.SubCategoryId, x.PriorityId, x.SeverityId, x.ImpactId,
            x.Description, x.AttachmentPath, x.SlaBreached,
            createdAt = x.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            slaDeadline = x.SlaDeadline.ToString("yyyy-MM-dd HH:mm"),
            branchName = x.Branch.Name
        }).ToList();

        return Json(new { total, page, pageSize, items });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ItTicket model, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(model.Subject)) return BadRequest(new { message = "Subject is required." });
        if (string.IsNullOrWhiteSpace(model.Description)) return BadRequest(new { message = "Description is required." });

        if (model.Id == 0)
        {
            var count = await db.ItTickets.CountAsync(ct);
            model.TicketNumber = $"TKT-{(count + 1):D6}";
            model.Requester = currentUser.UserName;
            model.BranchId = currentUser.BranchId ?? 1;
            model.Status = "New";
            model.CreatedAt = DateTime.UtcNow;
            model.CreatedBy = currentUser.UserName;

            // Calculate SLA Deadline
            var slaPolicy = await db.ItSlaPolicies
                .FirstOrDefaultAsync(x => x.PriorityId == model.PriorityId && x.IsActive && !x.IsDeleted, ct);
            var resolutionHours = slaPolicy?.ResolutionTimeHours ?? 48; // fallback 48h
            model.SlaDeadline = DateTime.UtcNow.AddHours(resolutionHours);

            db.ItTickets.Add(model);
        }
        else
        {
            var existing = await db.ItTickets.FindAsync(new object[] { model.Id }, ct);
            if (existing == null || existing.IsDeleted) return NotFound(new { message = "Ticket not found." });
            
            // Engineers can update status and assignment, Requesters can edit subject/desc if ticket is new
            if (IsEngineer())
            {
                existing.Status = model.Status;
                existing.AssignedEngineer = model.AssignedEngineer;
                existing.PriorityId = model.PriorityId;
                existing.SeverityId = model.SeverityId;
                existing.ImpactId = model.ImpactId;
                existing.RootCauseId = model.RootCauseId;
                existing.ResolutionTypeId = model.ResolutionTypeId;
                existing.ResolutionText = model.ResolutionText;

                if (model.Status == "Resolved" || model.Status == "Closed")
                {
                    existing.ClosureDate = DateTime.UtcNow;
                    if (DateTime.UtcNow > existing.SlaDeadline)
                    {
                        existing.SlaBreached = true;
                    }
                }
            }
            else if (existing.Requester == currentUser.UserName && existing.Status == "New")
            {
                existing.Subject = model.Subject;
                existing.Description = model.Description;
                existing.CategoryId = model.CategoryId;
                existing.SubCategoryId = model.SubCategoryId;
            }
            else
            {
                return Forbid();
            }

            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Help Desk Ticket saved successfully.", ticketNumber = model.TicketNumber });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var ticket = await db.ItTickets
            .Include(t => t.Branch)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        var masterIds = new[] {
            ticket.DepartmentId, ticket.CategoryId, ticket.SubCategoryId, ticket.PriorityId,
            ticket.SeverityId, ticket.ImpactId, ticket.RootCauseId ?? 0, ticket.ResolutionTypeId ?? 0
        }.Distinct().ToList();

        var masterMap = await db.ItMasterDatas
            .Where(x => masterIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var comments = ticket.Comments
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new {
                c.CommentText,
                c.IsInternal,
                createdAt = c.CreatedAt.ToString("dd MMM yyyy HH:mm"),
                createdBy = c.CreatedBy
            }).ToList();

        var ticketData = new {
            ticket.Id,
            ticket.TicketNumber,
            ticket.Requester,
            ticket.Subject,
            ticket.Description,
            ticket.Status,
            ticket.AssignedEngineer,
            ticket.AttachmentPath,
            ticket.ResolutionText,
            ticket.UserFeedbackScore,
            ticket.UserFeedbackText,
            ticket.SlaBreached,
            branchName = ticket.Branch.Name,
            department = masterMap.TryGetValue(ticket.DepartmentId, out var dept) ? dept : "Other",
            category = masterMap.TryGetValue(ticket.CategoryId, out var cat) ? cat : "Other",
            subCategory = masterMap.TryGetValue(ticket.SubCategoryId, out var sub) ? sub : "Other",
            priority = masterMap.TryGetValue(ticket.PriorityId, out var pri) ? pri : "Medium",
            severity = masterMap.TryGetValue(ticket.SeverityId, out var sev) ? sev : "Medium",
            impact = masterMap.TryGetValue(ticket.ImpactId, out var imp) ? imp : "Low",
            rootCause = ticket.RootCauseId.HasValue && masterMap.TryGetValue(ticket.RootCauseId.Value, out var rc) ? rc : string.Empty,
            resolutionType = ticket.ResolutionTypeId.HasValue && masterMap.TryGetValue(ticket.ResolutionTypeId.Value, out var rt) ? rt : string.Empty,
            ticket.DepartmentId, ticket.CategoryId, ticket.SubCategoryId, ticket.PriorityId, ticket.SeverityId, ticket.ImpactId, ticket.RootCauseId, ticket.ResolutionTypeId,
            createdAt = ticket.CreatedAt.ToString("dd MMM yyyy HH:mm"),
            slaDeadline = ticket.SlaDeadline.ToString("yyyy-MM-dd HH:mm"),
            closureDate = ticket.ClosureDate?.ToString("dd MMM yyyy HH:mm") ?? string.Empty,
            comments
        };

        return Json(ticketData);
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(int ticketId, string commentText, bool isInternal, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(commentText)) return BadRequest(new { message = "Comment text cannot be empty." });

        var ticket = await db.ItTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null || ticket.IsDeleted) return NotFound(new { message = "Ticket not found." });

        var comment = new ItTicketComment {
            TicketId = ticketId,
            CommentText = commentText,
            IsInternal = isInternal,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        db.ItTicketComments.Add(comment);

        // Auto-progress status from New/Assigned to In Progress on comment add
        if (ticket.Status == "New" || ticket.Status == "Assigned")
        {
            ticket.Status = "InProgress";
            ticket.UpdatedAt = DateTime.UtcNow;
            ticket.UpdatedBy = currentUser.UserName;
        }

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Comment added successfully." });
    }

    [HttpPost]
    public async Task<IActionResult> CloseTicket(int ticketId, int score, string feedback, CancellationToken ct)
    {
        var ticket = await db.ItTickets.FindAsync(new object[] { ticketId }, ct);
        if (ticket == null || ticket.IsDeleted) return NotFound(new { message = "Ticket not found." });

        if (ticket.Requester != currentUser.UserName && !IsHO()) return Forbid();

        ticket.Status = "Closed";
        ticket.UserFeedbackScore = score;
        ticket.UserFeedbackText = feedback;
        ticket.ClosureDate = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Ticket closed successfully with feedback." });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        if (!IsHO()) return Forbid();
        var ticket = await db.ItTickets.FindAsync(new object[] { id }, ct);
        if (ticket == null) return NotFound(new { message = "Ticket not found." });

        ticket.IsDeleted = true;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.UpdatedBy = currentUser.UserName;

        await db.SaveChangesAsync(ct);
        return Json(new { ok = true, message = "Ticket deleted successfully." });
    }
}
