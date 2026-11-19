using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.ShipStation;
using HKPartsEcommerce.Infrastructure.Security;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The ShipStation "Custom Store" endpoint (docs/shipstation-verification-requirements.md §3, §6.1). ShipStation
/// polls <c>GET /shipstation?action=export</c> for orders and posts <c>action=shipnotify</c> when it ships one.
/// Not behind the admin JWT: the caller signs in with the ShipStation credentials from the admin screen, sent
/// as HTTP Basic auth (SS-04), or as the legacy <c>SS-UserName</c> / <c>SS-Password</c> query parameters while
/// "Accept query-string credentials" is on. HTTPS only outside Development (SS-54); rate-limited with a
/// lockout after repeated failed logins (SS-53). Every call is logged without credentials or PII (SS-51).
/// </summary>
[Route("shipstation")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class ShipStationController : ApiControllerBase
{
    private readonly ShipStationRateLimiter _limiter;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ShipStationController> _logger;

    public ShipStationController(ShipStationRateLimiter limiter, IWebHostEnvironment env, ILogger<ShipStationController> logger)
    {
        _limiter = limiter;
        _env = env;
        _logger = logger;
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Endpoint(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var action = (Request.Query["action"].ToString() ?? string.Empty).Trim().ToLowerInvariant();

        if (!Request.IsHttps && !_env.IsDevelopment())
            return Refuse(StatusCodes.Status403Forbidden, "SHIPSTATION_HTTPS_REQUIRED", "HTTPS is required.");

        if (_limiter.IsLockedOut(ip) || !_limiter.TryConsume(ip))
            return Refuse(StatusCodes.Status429TooManyRequests, "SHIPSTATION_RATE_LIMITED", "Too many requests. Try again later.");

        var (username, password, fromQuery) = ReadCredentials();
        var auth = await Mediator.Send(new AuthenticateShipStationQuery(username, password, fromQuery), ct);
        switch (auth.Result)
        {
            case ShipStationAuthResult.Disabled:
                await LogAsync(ShipStationActions.Auth, StatusCodes.Status403Forbidden, stopwatch, ip, error: "ShipStation is disabled.", ct: ct);
                return Refuse(StatusCodes.Status403Forbidden, "SHIPSTATION_DISABLED", "The ShipStation integration is disabled.");
            case ShipStationAuthResult.MissingCredentials:
                _limiter.RecordFailure(ip);
                await LogAsync(ShipStationActions.Auth, StatusCodes.Status401Unauthorized, stopwatch, ip, error: "No credentials.", ct: ct);
                Response.Headers.WWWAuthenticate = "Basic realm=\"ShipStation\"";
                return Refuse(StatusCodes.Status401Unauthorized, "SHIPSTATION_UNAUTHORIZED", "Credentials are required.");
            case ShipStationAuthResult.BadCredentials:
                _limiter.RecordFailure(ip);
                await LogAsync(ShipStationActions.Auth, StatusCodes.Status403Forbidden, stopwatch, ip, error: "Wrong username or password.", ct: ct);
                return Refuse(StatusCodes.Status403Forbidden, "SHIPSTATION_UNAUTHORIZED", "Invalid credentials.");
        }
        _limiter.RecordSuccess(ip);

        return action switch
        {
            "export" => await ExportAsync(auth, stopwatch, ip, ct),
            "shipnotify" => await ShipNotifyAsync(auth, stopwatch, ip, ct),
            _ => Refuse(StatusCodes.Status400BadRequest, "SHIPSTATION_BAD_ACTION", "action must be export or shipnotify."),
        };
    }

    private async Task<IActionResult> ExportAsync(ShipStationAuthOutcome auth, Stopwatch stopwatch, string? ip, CancellationToken ct)
    {
        int? page = int.TryParse(Request.Query["page"], out var p) ? p : null;
        ShipStationExportResult result;
        try
        {
            result = await Mediator.Send(new ShipStationExportQuery(
                Request.Query["start_date"].ToString(), Request.Query["end_date"].ToString(), page), ct);
        }
        catch (Application.Common.Exceptions.ValidationException ex)
        {
            await LogAsync(ShipStationActions.Export, StatusCodes.Status400BadRequest, stopwatch, ip, page: page, error: ex.Message, ct: ct);
            throw;
        }

        if (result.MissingWeightSkus.Count > 0)
            _logger.LogWarning("ShipStation export: {Count} item(s) exported with no weight (SKUs: {Skus}). Fix the product weights.",
                result.MissingWeightSkus.Count, string.Join(", ", result.MissingWeightSkus.Distinct().Take(50)));
        if (auth.DebugLogging)
            _logger.LogInformation("ShipStation export debug payload (page {Page}/{Pages}): {Xml}", result.Page, result.Pages, result.Xml);

        await LogAsync(ShipStationActions.Export, StatusCodes.Status200OK, stopwatch, ip,
            start: result.StartUtc, end: result.EndUtc, page: result.Page, orderCount: result.OrderCount, ct: ct);
        return Content(result.Xml, "text/xml; charset=utf-8", Encoding.UTF8);
    }

    private async Task<IActionResult> ShipNotifyAsync(ShipStationAuthOutcome auth, Stopwatch stopwatch, string? ip, CancellationToken ct)
    {
        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct);
        if (auth.DebugLogging)
            _logger.LogInformation("ShipStation shipnotify debug payload: {Query} {Body}", SafeQuery(), body);

        var result = await Mediator.Send(new ShipStationShipNotifyCommand(
            Request.Query["order_number"].ToString(),
            Request.Query["carrier"].ToString(),
            Request.Query["service"].ToString(),
            Request.Query["tracking_number"].ToString(),
            body), ct);

        var status = result.Outcome switch
        {
            ShipNotifyOutcome.NotFound => StatusCodes.Status404NotFound,
            ShipNotifyOutcome.Rejected => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status200OK,
        };
        if (status != StatusCodes.Status200OK)
            _logger.LogWarning("ShipStation shipnotify for {OrderNumber}: {Outcome} — {Message}", result.OrderNumber, result.Outcome, result.Message);

        await LogAsync(ShipStationActions.ShipNotify, status, stopwatch, ip,
            orderNumber: result.OrderNumber, orderCount: result.ItemsShipped,
            error: status == StatusCodes.Status200OK
                ? (result.Outcome == ShipNotifyOutcome.Duplicate ? "Duplicate notice ignored." : null)
                : result.Message,
            ct: ct);

        // SS-44: 200 with an empty body on success.
        return result.Outcome switch
        {
            ShipNotifyOutcome.NotFound => Refuse(status, "ORDER_NOT_FOUND", result.Message ?? "Order not found."),
            ShipNotifyOutcome.Rejected => Refuse(status, "ORDER_NOT_SHIPPABLE", result.Message ?? "The order cannot be shipped."),
            _ => new EmptyResult(),
        };
    }

    /// <summary>Basic auth first (SS-04); the legacy query parameters as a fallback.</summary>
    private (string? Username, string? Password, bool FromQuery) ReadCredentials()
    {
        var header = Request.Headers.Authorization.ToString();
        if (AuthenticationHeaderValue.TryParse(header, out var value)
            && string.Equals(value.Scheme, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(value.Parameter))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(value.Parameter));
                var colon = decoded.IndexOf(':');
                if (colon > 0)
                    return (decoded[..colon], decoded[(colon + 1)..], false);
            }
            catch (FormatException)
            {
                // Not base64: treated as no credentials.
            }
        }

        var user = Request.Query["SS-UserName"].ToString();
        var pass = Request.Query["SS-Password"].ToString();
        return string.IsNullOrEmpty(user) && string.IsNullOrEmpty(pass)
            ? (null, null, false)
            : (user, pass, true);
    }

    /// <summary>The query string with the legacy password removed, for debug logging (SS-51).</summary>
    private string SafeQuery() =>
        string.Join("&", Request.Query
            .Where(q => !q.Key.Equals("SS-Password", StringComparison.OrdinalIgnoreCase))
            .Select(q => $"{q.Key}={q.Value}"));

    private ObjectResult Refuse(int status, string code, string message) =>
        StatusCode(status, new ProblemDetails
        {
            Status = status,
            Title = message,
            Extensions = { ["code"] = code },
        });

    private Task LogAsync(
        string action, int status, Stopwatch stopwatch, string? ip,
        DateTime? start = null, DateTime? end = null, int? page = null, int? orderCount = null,
        string? orderNumber = null, string? error = null, CancellationToken ct = default) =>
        Mediator.Send(new LogShipStationCallCommand(
            action, status, stopwatch.ElapsedMilliseconds, start, end, page, orderCount, orderNumber, error, ip), ct);
}
