using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IncentivePortal.Controllers;

[Authorize(Roles = "Super Admin,HO Finance")]
public sealed class AnnouncementsController(IncentiveDbContext db, IMemoryCache cache) : Controller
{
    // Cache key — must match exactly what _Layout.cshtml uses
    private const string AnnouncementCacheKey = "layout_announcements";

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var list = await db.Announcements
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return View(list);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string message, string severity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Json(new { ok = false, message = "Announcement message cannot be empty." });

        var announcement = new Announcement
        {
            Message = message.Trim(),
            Severity = severity ?? "info",
            IsActive = true
        };

        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(cancellationToken);

        // Fix Issue 3: bust the shared layout cache so all users see the new
        // announcement on their very next page request, not up to 5 min later.
        cache.Remove(AnnouncementCacheKey);

        return Json(new { ok = true, message = "Announcement posted successfully." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id, CancellationToken cancellationToken)
    {
        var announcement = await db.Announcements.FindAsync([id], cancellationToken);
        if (announcement == null)
            return Json(new { ok = false, message = "Announcement not found." });

        announcement.IsActive = !announcement.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        // Bust cache so toggle takes effect immediately in the layout ticker
        cache.Remove(AnnouncementCacheKey);

        return Json(new { ok = true, message = $"Announcement is now {(announcement.IsActive ? "Active" : "Inactive")}." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var announcement = await db.Announcements.FindAsync([id], cancellationToken);
        if (announcement == null)
            return Json(new { ok = false, message = "Announcement not found." });

        announcement.IsDeleted = true;
        await db.SaveChangesAsync(cancellationToken);

        // Bust cache so deleted announcement disappears immediately for all users
        cache.Remove(AnnouncementCacheKey);

        return Json(new { ok = true, message = "Announcement deleted successfully." });
    }
}
