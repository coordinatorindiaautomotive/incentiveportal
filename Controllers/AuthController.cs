using IncentivePortal.DTOs;
using IncentivePortal.Services;
using IncentivePortal.Helpers;
using IncentivePortal.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IncentivePortal.Controllers;

public sealed class AuthController(
    IAuthService authService,
    INotificationService notificationService,
    IPasswordHasher hasher,
    IncentiveDbContext db,
    IAuditEngineService auditEngine) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);
        var ok = await authService.SignInCookieAsync(HttpContext, new LoginRequest(model.UserName, model.Password), cancellationToken);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }
        return LocalRedirect(model.ReturnUrl ?? Url.Action("Dashboard", "Home")!);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        var username = User.Identity?.Name ?? "system";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await auditEngine.LogActionAsync("Logout", "User", username, "{}", "{}", username, ip);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string usernameOrEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(usernameOrEmail))
        {
            ModelState.AddModelError(string.Empty, "Please enter your username or email address.");
            return View();
        }

        var user = await db.Users
            .FirstOrDefaultAsync(u => (u.UserName == usernameOrEmail || u.Email == usernameOrEmail) && !u.IsDeleted, cancellationToken);

        if (user == null)
        {
            ViewBag.Message = "If the account exists, a temporary password has been sent to the registered destination.";
            return View();
        }

        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8);
        var (hash, salt) = hasher.HashPassword(tempPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        await db.SaveChangesAsync(cancellationToken);

        var sent = await notificationService.SendPasswordResetNotificationAsync(usernameOrEmail, tempPassword, cancellationToken);
        if (sent)
        {
            ViewBag.Message = "A temporary password has been successfully sent to your registered email or mobile.";
        }
        else
        {
            ViewBag.Error = "We found your account, but there was an error sending the notification. Please contact the administrator.";
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}

public sealed class LoginViewModel
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReturnUrl { get; set; }
}
