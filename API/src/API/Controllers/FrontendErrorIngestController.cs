using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Serilog.Context;
using HKPartsEcommerce.API.Logging;
using HKPartsEcommerce.API.Middleware;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Body of a browser error report posted to the frontend error ingestion endpoint (WO-71).</summary>
public sealed record FrontendErrorRequest(string? Message, string? Stack, string? Url);

/// <summary>
/// Public, rate-limited ingestion of uncaught client-side JS errors (WO-71). Each report is persisted as
/// an Error-level ApplicationLog tagged <c>Source = "Frontend"</c> by reusing the existing Serilog →
/// ApplicationLogs pipeline: Source/Route/CorrelationId are pushed as log-context properties (matching
/// the MSSQL sink's AdditionalColumns) and the browser stack rides into the Exception column via a
/// <see cref="FrontendReportedException"/> carrier — keeping the Message column a clean summary. A
/// lightweight in-memory sliding-window limiter keyed by client IP protects the endpoint from floods
/// (HTTP 429 when exceeded).
/// </summary>
[Route("api/logging")]
[AllowAnonymous]
public class FrontendErrorIngestController : ApiControllerBase
{
    // Sliding-window rate limit: at most MaxReportsPerWindow reports per client IP per Window.
    private const int MaxReportsPerWindow = 20;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
    private const string RateLimitKeyPrefix = "fe-error-rl:";

    // Defensive caps matching the MSSQL sink's fixed-length AdditionalColumns (Route = 500, CorrelationId
    // = 64) so an over-length browser value can never fail the sink's batched insert.
    private const int RouteMaxLength = 500;
    private const int CorrelationIdMaxLength = 64;

    private const string CorrelationIdItemKey = "CorrelationId";

    private readonly ILogger<FrontendErrorIngestController> _logger;
    private readonly IMemoryCache _cache;

    public FrontendErrorIngestController(ILogger<FrontendErrorIngestController> logger, IMemoryCache cache)
    {
        _logger = logger;
        _cache = cache;
    }

    /// <summary>Ingest a single uncaught frontend error. Returns 429 when the caller exceeds the rate limit.</summary>
    [HttpPost("frontend-error")]
    public IActionResult FrontendError([FromBody] FrontendErrorRequest? request)
    {
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (IsRateLimited(_cache, clientIp))
            return StatusCode(StatusCodes.Status429TooManyRequests);

        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest();

        var route = Truncate(request.Url, RouteMaxLength);
        var correlationId = Truncate(ResolveCorrelationId(), CorrelationIdMaxLength);

        // The stack rides in a carrier exception so it lands in the Exception column (via the MSSQL sink),
        // leaving the templated Message column a clean, summary-only value.
        var carrier = new FrontendReportedException(request.Message, request.Stack);

        using (LogContext.PushProperty("Source", "Frontend"))
        using (LogContext.PushProperty("Route", route))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogError(carrier, "{FrontendMessage}", request.Message);
        }

        return Ok();
    }

    /// <summary>Correlation id from the inbound header, falling back to the value set by CorrelationIdMiddleware.</summary>
    private string? ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var header)
            && !string.IsNullOrWhiteSpace(header))
            return header.ToString();

        return HttpContext.Items.TryGetValue(CorrelationIdItemKey, out var value)
            && value is string s && !string.IsNullOrWhiteSpace(s)
                ? s
                : null;
    }

    private static string? Truncate(string? value, int maxLength)
        => string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    // --- Self-contained in-memory sliding-window limiter over the already-registered IMemoryCache ---
    // Approximate by design (a brand-new key may race under a simultaneous burst); the per-bucket lock
    // makes the steady-state count correct, and the sliding cache expiry evicts idle IPs so it never leaks.

    private static bool IsRateLimited(IMemoryCache cache, string clientIp)
    {
        var bucket = cache.GetOrCreate(RateLimitKeyPrefix + clientIp, entry =>
        {
            entry.SlidingExpiration = Window;
            return new RateBucket();
        })!;

        lock (bucket.Gate)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - Window;
            while (bucket.Hits.Count > 0 && bucket.Hits.Peek() < cutoff)
                bucket.Hits.Dequeue();

            if (bucket.Hits.Count >= MaxReportsPerWindow)
                return true;

            bucket.Hits.Enqueue(now);
            return false;
        }
    }

    private sealed class RateBucket
    {
        public object Gate { get; } = new();
        public Queue<DateTime> Hits { get; } = new();
    }
}
