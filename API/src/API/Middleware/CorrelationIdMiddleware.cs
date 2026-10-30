using Serilog.Context;

namespace HKPartsEcommerce.API.Middleware;

/// <summary>
/// Ensures every request carries a correlation id: read from the inbound <c>X-Correlation-ID</c>
/// header or generated, pushed into the Serilog log context, and echoed on the response.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value)
            && !string.IsNullOrWhiteSpace(value)
            ? value.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
