using IncentivePortal.Data;
using IncentivePortal.Helpers;
using IncentivePortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

[Authorize(Roles = AppRoles.SuperAdmin)]
public sealed class UsersController(IncentiveDbContext db, IPasswordHasher hasher) : Controller
{
    // =========================================================
    // LIST USERS & METADATA
    // =========================================================
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Branches = await db.Branches.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        ViewBag.Roles = await db.Roles.OrderBy(x => x.Name).ToListAsync(cancellationToken);

        var users = await db.Users
            .Include(x => x.Branch)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.UserName)
            .ToListAsync(cancellationToken);

        return View(users);
    }

    // =========================================================
    // SAVE USER (CREATE / UPDATE)
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> Save(
        int id,
        string userName,
        string email,
        string? password,
        int? branchId,
        int roleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return BadRequest(new { message = "Username is required." });
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { message = "Email is required." });

        if (id == 0)
        {
            // CREATE NEW USER
            if (await db.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
                return BadRequest(new { message = "Username already exists." });
            if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
                return BadRequest(new { message = "Email already registered." });
            if (string.IsNullOrWhiteSpace(password))
                return BadRequest(new { message = "Password is required for new users." });

            var (hash, salt) = hasher.HashPassword(password);
            var user = new User
            {
                UserName = userName,
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt,
                BranchId = branchId,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);

            // Add Role Map
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            await db.SaveChangesAsync(cancellationToken);

            return Json(new { ok = true, message = $"User '{userName}' successfully created and mapped." });
        }
        else
        {
            // UPDATE EXISTING USER
            var user = await db.Users.Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (user == null)
                return NotFound(new { message = "User not found." });

            // Check email conflicts
            if (await db.Users.AnyAsync(x => x.Email == email && x.Id != id, cancellationToken))
                return BadRequest(new { message = "Email already registered by another user." });

            user.UserName = userName;
            user.Email = email;
            user.BranchId = branchId;

            if (!string.IsNullOrWhiteSpace(password))
            {
                var (hash, salt) = hasher.HashPassword(password);
                user.PasswordHash = hash;
                user.PasswordSalt = salt;
            }

            // Remap Role composite keys
            db.UserRoles.RemoveRange(user.UserRoles);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            await db.SaveChangesAsync(cancellationToken);

            return Json(new { ok = true, message = $"User '{userName}' successfully updated." });
        }
    }

    // =========================================================
    // DELETE / TOGGLE STATUS
    // =========================================================
    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int id, CancellationToken cancellationToken)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
            return NotFound(new { message = "User not found." });

        if (user.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "The default super administrator account status cannot be changed." });

        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync(cancellationToken);

        return Json(new { ok = true, message = $"User status set to {(user.IsActive ? "Active" : "Inactive")}." });
    }
}
