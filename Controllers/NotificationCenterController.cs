using IncentivePortal.Data;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class NotificationCenterController(IncentiveDbContext db, IMemoryCache memoryCache) : Controller
{
    public async Task<IActionResult> Index()
    {
        var username = User.Identity?.Name ?? "system";
        var notifications = await db.SystemNotifications
            .Where(n => (n.TargetUser == username || n.TargetUser == "all") && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> GetUnreadCount()
    {
        var username = User.Identity?.Name ?? "system";
        string cacheKey = $"UnreadCount_{username}";

        if (!memoryCache.TryGetValue(cacheKey, out int count))
        {
            count = await db.SystemNotifications
                .CountAsync(n => (n.TargetUser == username || n.TargetUser == "all") && !n.IsRead && !n.IsDeleted);

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(30));
            memoryCache.Set(cacheKey, count, cacheEntryOptions);
        }

        return Json(new { count });
    }

    [HttpGet]
    public async Task<IActionResult> GetNotificationsList()
    {
        var username = User.Identity?.Name ?? "system";
        string cacheKey = $"NotificationList_{username}";

        if (!memoryCache.TryGetValue(cacheKey, out object? items))
        {
            items = await db.SystemNotifications
                .Where(n => (n.TargetUser == username || n.TargetUser == "all") && !n.IsDeleted)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.IsRead,
                    n.NotificationType,
                    createdAt = n.CreatedAt.ToString("dd MMM HH:mm")
                })
                .ToListAsync();

            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(30));
            memoryCache.Set(cacheKey, items, cacheEntryOptions);
        }

        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var item = await db.SystemNotifications.FindAsync(id);
        if (item == null) return NotFound("Notification not found.");

        item.IsRead = true;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = User.Identity?.Name ?? "system";
        await db.SaveChangesAsync();

        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var username = User.Identity?.Name ?? "system";
        var items = await db.SystemNotifications
            .Where(n => (n.TargetUser == username || n.TargetUser == "all") && !n.IsRead && !n.IsDeleted)
            .ToListAsync();

        foreach (var item in items)
        {
            item.IsRead = true;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = username;
        }

        await db.SaveChangesAsync();
        return Json(new { ok = true, message = "All notifications marked as read." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    public async Task<IActionResult> Broadcast(string title, string message, string type)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            return BadRequest("Title and message are required.");

        var notification = new SystemNotification
        {
            TargetUser = "all",
            Title = title,
            Message = message,
            NotificationType = string.IsNullOrEmpty(type) ? "System" : type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };

        db.SystemNotifications.Add(notification);
        await db.SaveChangesAsync();

        return Json(new { ok = true, message = "Broadcast message sent successfully." });
    }
}
