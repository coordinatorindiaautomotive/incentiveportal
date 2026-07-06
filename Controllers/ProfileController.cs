using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using IncentivePortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize]
public sealed class ProfileController(
    IncentiveDbContext db, 
    IPasswordHasher hasher, 
    IAuditEngineService auditEngine) : Controller
{
    // =========================================================
    // VIEW PROFILE
    // =========================================================
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
            return RedirectToAction("Login", "Auth");

        var user = await db.Users
            .Include(u => u.Branch)
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);

        if (user == null)
            return RedirectToAction("Login", "Auth");

        // Fetch last 15 audit logs related to this user
        var recentLogs = await db.AuditLogs
            .AsNoTracking()
            .Where(l => l.ChangedBy == userName)
            .OrderByDescending(l => l.ChangedAt)
            .Take(15)
            .ToListAsync(cancellationToken);

        ViewBag.RecentLogs = recentLogs;
        return View(user);
    }

    // =========================================================
    // UPDATE PROFILE DETAILS (EMAIL)
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmail(string email, CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
            return Unauthorized(new { message = "Session expired. Please log in again." });

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email address is required." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user == null)
            return NotFound(new { message = "User account not found." });

        // Check if email already used by another active user
        var exists = await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id && !u.IsDeleted, cancellationToken);
        if (exists)
            return BadRequest(new { message = "This email address is already registered to another account." });

        var oldEmail = user.Email;
        user.Email = email;
        
        await db.SaveChangesAsync(cancellationToken);

        // Audit log
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await auditEngine.LogActionAsync("UpdateEmail", "User", user.UserName, $"{{\"Email\":\"{oldEmail}\"}}", $"{{\"Email\":\"{email}\"}}", user.UserName, ip);

        return Json(new { ok = true, message = "Email address updated successfully." });
    }

    // =========================================================
    // CHANGE PASSWORD
    // =========================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword, CancellationToken cancellationToken)
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
            return Unauthorized(new { message = "Session expired. Please log in again." });

        if (string.IsNullOrWhiteSpace(currentPassword))
            return BadRequest(new { message = "Current password is required." });

        if (string.IsNullOrWhiteSpace(newPassword))
            return BadRequest(new { message = "New password is required." });

        if (newPassword.Length < 6)
            return BadRequest(new { message = "New password must be at least 6 characters long." });

        if (newPassword != confirmPassword)
            return BadRequest(new { message = "New password and confirmation do not match." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, cancellationToken);
        if (user == null)
            return NotFound(new { message = "User account not found." });

        // Verify current password
        if (!hasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt))
            return BadRequest(new { message = "The current password you entered is incorrect." });

        // Update password hash/salt
        var (hash, salt) = hasher.HashPassword(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await db.SaveChangesAsync(cancellationToken);

        // Audit log
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await auditEngine.LogActionAsync("ChangePassword", "User", user.UserName, "{}", "{}", user.UserName, ip);

        return Json(new { ok = true, message = "Your password has been changed successfully." });
    }
}
