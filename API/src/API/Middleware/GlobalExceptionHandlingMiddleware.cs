using HKPartsEcommerce.Application.Common.Exceptions;

namespace HKPartsEcommerce.API.Middleware;

/// <summary>
/// Catches all unhandled exceptions, logs them with the correlation id and full stack trace, and
/// returns a structured error response that exposes only the correlation id — never a stack trace
/// (API Server blueprint, GlobalExceptionHandler contract).
/// </summary>
public class GlobalExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlingMiddleware> _logger;

    public GlobalExceptionHandlingMiddleware(RequestDelegate next, ILogger<GlobalExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;

        (int status, string title, IDictionary<string, string[]>? errors) = ex switch
        {
            ValidationException ve => (StatusCodes.Status400BadRequest, "One or more validation errors occurred.", ve.Errors),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, ex.Message, null),
            NotFoundException => (StatusCodes.Status404NotFound, ex.Message, null),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "You do not have permission to perform this action.", null),
            ConflictException => (StatusCodes.Status409Conflict, ex.Message, null),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null),
        };

        if (status >= 500)
            _logger.LogError(ex, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);
        else
            _logger.LogWarning(ex, "Request failed with {Status}. CorrelationId={CorrelationId}", status, correlationId);

        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        var payload = new Dictionary<string, object?>
        {
            ["type"] = $"https://httpstatuses.io/{status}",
            ["title"] = title,
            ["status"] = status,
            ["correlationId"] = correlationId,
        };
        if (errors is not null)
            payload["errors"] = errors;

        await context.Response.WriteAsJsonAsync(payload);
    }
}
