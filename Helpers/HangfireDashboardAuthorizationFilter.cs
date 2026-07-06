using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace IncentivePortal.Helpers;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true && httpContext.User.IsInRole("Super Admin");
    }
}
