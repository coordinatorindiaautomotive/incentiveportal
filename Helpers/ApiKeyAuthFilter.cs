using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace IncentivePortal.Helpers;

public sealed class ApiKeyAuthAttribute : TypeFilterAttribute
{
    public ApiKeyAuthAttribute() : base(typeof(ApiKeyAuthFilter))
    {
    }
}

public sealed class ApiKeyAuthFilter(IConfiguration configuration) : IAsyncActionFilter
{
    private const string ApiKeyHeaderName = "X-API-KEY";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "API Key is missing" });
            return;
        }

        var apiKey = configuration.GetValue<string>("AiSettings:ApiKey") ?? "AI_INCENTIVE_KEY_9999";

        if (!apiKey.Equals(extractedApiKey))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Unauthorized client" });
            return;
        }

        await next();
    }
}
