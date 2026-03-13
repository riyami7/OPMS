using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace OperationalPlanMS.Filters
{
    /// <summary>
    /// API Key authentication for /api/data/* endpoints.
    /// Reads key from X-API-Key header and validates against appsettings.
    /// Does NOT affect cookie-based auth for the web UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ApiKeyAuthAttribute : Attribute, IAsyncActionFilter
    {
        private const string ApiKeyHeaderName = "X-API-Key";

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey))
            {
                context.Result = new UnauthorizedObjectResult(new { error = "API key missing. Provide X-API-Key header." });
                return;
            }

            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var validKey = config["ApiSettings:ApiKey"];

            if (string.IsNullOrEmpty(validKey) || providedKey != validKey)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key." });
                return;
            }

            await next();
        }
    }
}
