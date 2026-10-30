using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.API.Middleware;

/// <summary>
/// Until first-run setup completes, blocks admin/tenant API calls (returning 423 with a
/// setupRequired flag) while allowing the setup and auth endpoints, health, and docs. The Client App
/// uses this to route users into the setup wizard (Platform Foundation, SetupWizardController).
/// </summary>
public class SetupGuardMiddleware
{
    private readonly RequestDelegate _next;

    public SetupGuardMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context, ISettingsService settings)
    {
        if (IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var completed = await settings.GetAsync<bool>("setup.completed");
        if (!completed)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.io/423",
                title = "First-run setup is required before using this endpoint.",
                status = StatusCodes.Status423Locked,
                setupRequired = true,
                correlationId = context.Items["CorrelationId"] as string,
            });
            return;
        }

        await _next(context);
    }

    private static bool IsExempt(PathString path)
    {
        // Only guard the JSON API; health, OpenAPI, and Scalar live outside /api.
        if (!path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        return path.StartsWithSegments("/api/setup", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase);
    }
}
