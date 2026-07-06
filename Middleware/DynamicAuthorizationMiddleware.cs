using System.Security.Claims;
using IncentivePortal.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace IncentivePortal.Middleware;

public sealed class DynamicAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public DynamicAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IncentiveDbContext db, IMemoryCache cache)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint == null)
        {
            await _next(context);
            return;
        }

        var routeData = context.GetRouteData();
        var controller = routeData?.Values["controller"]?.ToString() ?? string.Empty;
        var action = routeData?.Values["action"]?.ToString() ?? string.Empty;

        // Skip auth checks if endpoint allows anonymous access
        var hasAllowAnonymous = endpoint.Metadata.GetMetadata<AllowAnonymousAttribute>() != null;
        if (hasAllowAnonymous || !context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Always allow Super Admin to bypass all dynamic checks
        if (context.User.IsInRole("Super Admin"))
        {
            await _next(context);
            return;
        }

        var roles = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var genericAction = GetGenericActionName(action, context.Request.Method);

        // Cache the full RolePermissions set per role-combination (2-minute absolute expiration).
        // This avoids hitting SQL on every single authenticated request.
        var cacheVersion = cache.GetOrCreate("roleperm_version", _ => Guid.NewGuid().ToString());
        var cacheKey = $"roleperm:{cacheVersion}:{string.Join(",", roles.OrderBy(r => r))}";
        if (!cache.TryGetValue(cacheKey, out List<CachedPermission>? allPerms))
        {
            var rawPerms = await db.RolePermissions
                .AsNoTracking()
                .Where(p => !p.IsDeleted && roles.Contains(p.RoleName))
                .Select(p => new CachedPermission
                {
                    Module = p.Module,
                    Action = p.Action,
                    IsAllowed = p.IsAllowed
                })
                .ToListAsync();
            allPerms = rawPerms;
            cache.Set(cacheKey, allPerms, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });
        }

        // Filter cached permissions for this controller/action
        var dbPermissions = allPerms!
            .Where(p => p.Module == controller &&
                       (p.Action == action || p.Action == genericAction || p.Action == "Access"))
            .ToList();

        // 1. Dynamic DENY check: If any role has a dynamic block, return 403 Forbidden
        if (dbPermissions.Any(p => !p.IsAllowed))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Forbidden: Access denied by Control Tower governance rules.");
            return;
        }

        // 2. Dynamic ALLOW check: Check if user needs dynamic role bypass for hardcoded [Authorize(Roles = "...")]
        var authorizeAttributes = endpoint.Metadata.GetOrderedMetadata<AuthorizeAttribute>();
        if (authorizeAttributes != null && authorizeAttributes.Any())
        {
            var userHasRequiredRole = false;
            var allRequiredRoles = new List<string>();

            foreach (var attr in authorizeAttributes)
            {
                if (string.IsNullOrEmpty(attr.Roles))
                {
                    userHasRequiredRole = true;
                    break;
                }

                var reqRoles = attr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                allRequiredRoles.AddRange(reqRoles);

                if (reqRoles.Any(r => context.User.IsInRole(r)))
                {
                    userHasRequiredRole = true;
                }
            }

            if (!userHasRequiredRole && allRequiredRoles.Count > 0)
            {
                // User does not meet hardcoded roles. Check if they have a dynamic ALLOW rule.
                var isDynamicallyAllowed = dbPermissions.Any(p => p.IsAllowed);
                if (isDynamicallyAllowed)
                {
                    // Dynamically inject the first required role claim to satisfy the hardcoded Auth check
                    var identity = (ClaimsIdentity)context.User.Identity!;
                    identity.AddClaim(new Claim(ClaimTypes.Role, allRequiredRoles.First()));
                }
            }
        }

        await _next(context);
    }

    private static string GetGenericActionName(string action, string httpMethod)
    {
        var actLower = action.ToLowerInvariant();
        
        if (actLower.Contains("delete") || actLower.Contains("remove") || actLower.Contains("destroy"))
            return "Delete";
            
        if (actLower.Contains("upload") || actLower.Contains("import") || actLower.Contains("preview") || actLower.Contains("commit") || actLower.Contains("rollback"))
            return "Upload";
            
        if (actLower.Contains("save") || actLower.Contains("edit") || actLower.Contains("update") || actLower.Contains("create") || actLower.Contains("toggle") || actLower.Contains("process") || actLower.Contains("post"))
            return "Edit";
            
        if (actLower.Contains("view") || actLower.Contains("get") || actLower.Contains("details") || actLower.Contains("list") || actLower.Contains("register") || actLower.Contains("report"))
            return "View";

        if (httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) || 
            httpMethod.Equals("PUT", StringComparison.OrdinalIgnoreCase) || 
            httpMethod.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            return "Edit";
        }

        return "Access";
    }
}

public sealed class CachedPermission
{
    public string Module { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}
