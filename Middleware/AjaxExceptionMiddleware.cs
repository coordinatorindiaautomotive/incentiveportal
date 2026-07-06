using System.Text.Json;

namespace IncentivePortal.Middleware;

public sealed class AjaxExceptionMiddleware(
    RequestDelegate next,
    ILogger<AjaxExceptionMiddleware> logger,
    IWebHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (IsAjaxOrApi(context))
        {
            logger.LogError(ex, "Request failed: {Path}", context.Request.Path);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            // Fix Issue 4 — never leak stack traces or internal exception chains in production.
            // Development: full exception message for debugging.
            // Production: generic user-facing message; full detail is in the server log only.
            var message = env.IsProduction()
                ? "An unexpected error occurred. Please try again or contact support."
                : ex.GetBaseException().Message;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
        }
    }

    private static bool IsAjaxOrApi(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api")
           || context.Request.Headers.XRequestedWith == "XMLHttpRequest"
           || context.Request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);
}
