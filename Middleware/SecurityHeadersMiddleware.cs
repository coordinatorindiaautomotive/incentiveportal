namespace IncentivePortal.Middleware;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            headers.TryAdd("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://cdn.datatables.net https://unpkg.com https://cdn.tailwindcss.com 'unsafe-inline'; " +
                "style-src 'self' https://cdnjs.cloudflare.com https://cdn.datatables.net https://fonts.googleapis.com https://cdn.jsdelivr.net 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +
                "frame-ancestors 'self';");
            return Task.CompletedTask;
        });

        await next(context);
    }
}
