using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using IncentivePortal.Data;
using IncentivePortal.DTOs;
using IncentivePortal.Helpers;
using IncentivePortal.Models;

namespace IncentivePortal.Services;

/// <summary>
/// Service interface handling portal authentication for both session cookie and JWT API calls.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates API user and returns JWT Token containing user roles and branch context.
    /// </summary>
    Task<LoginResponse> LoginApiAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates web user and signs them in using Cookie authentication.
    /// </summary>
    Task<bool> SignInCookieAsync(HttpContext httpContext, LoginRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Sealed implementation of <see cref="IAuthService"/> with secure PBKDF2 hashing verification.
/// </summary>
public sealed class AuthService(
    IncentiveDbContext db,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwt,
    IAuditEngineService auditEngine,
    IHttpContextAccessor httpContextAccessor
) : IAuthService
{
    public async Task<LoginResponse> LoginApiAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var ip = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserName == request.UserName && x.IsActive, cancellationToken);

        if (user is null)
        {
            await auditEngine.LogActionAsync("LoginFailed", "User", request.UserName, "{}", "{}", "system", ip);
            return new LoginResponse(false, null, "Invalid username or password.");
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            var remainingMin = Math.Ceiling((user.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes);
            await auditEngine.LogActionAsync("LoginBlockedLockout", "User", user.UserName, "{}", "{}", user.UserName, ip);
            return new LoginResponse(false, null, $"Account is temporarily locked. Please try again in {remainingMin} minutes.");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                await auditEngine.LogActionAsync("AccountLocked", "User", user.UserName, "{}", "{}", user.UserName, ip);
            }
            else
            {
                await auditEngine.LogActionAsync("LoginFailed", "User", user.UserName, "{}", "{}", user.UserName, ip);
            }
            await db.SaveChangesAsync(cancellationToken);
            return new LoginResponse(false, null, "Invalid username or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditEngine.LogActionAsync("LoginSuccess", "User", user.UserName, "{}", "{}", user.UserName, ip);

        return new LoginResponse(true, jwt.Generate(user, user.UserRoles.Select(x => x.Role.Name)), "Login successful.");
    }

    public async Task<bool> SignInCookieAsync(HttpContext httpContext, LoginRequest request, CancellationToken cancellationToken = default)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        var user = await db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserName == request.UserName && x.IsActive, cancellationToken);

        if (user is null)
        {
            await auditEngine.LogActionAsync("LoginFailed", "User", request.UserName, "{}", "{}", "system", ip);
            return false;
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            await auditEngine.LogActionAsync("LoginBlockedLockout", "User", user.UserName, "{}", "{}", user.UserName, ip);
            return false;
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(15);
                await auditEngine.LogActionAsync("AccountLocked", "User", user.UserName, "{}", "{}", user.UserName, ip);
            }
            else
            {
                await auditEngine.LogActionAsync("LoginFailed", "User", user.UserName, "{}", "{}", user.UserName, ip);
            }
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        await auditEngine.LogActionAsync("LoginSuccess", "User", user.UserName, "{}", "{}", user.UserName, ip);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("branchId", user.BranchId?.ToString() ?? string.Empty)
        };
        claims.AddRange(user.UserRoles.Select(x => new Claim(ClaimTypes.Role, x.Role.Name)));
        await httpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return true;
    }
}
