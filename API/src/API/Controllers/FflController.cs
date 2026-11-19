using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Ffl;
using HKPartsEcommerce.Infrastructure.Security;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// FFL dealer step of the storefront checkout (FFL shipping requirements §6.1). The browser only ever calls
/// these endpoints: the directory is queried server-side and no directory credentials reach the page
/// (FFL-110). Searches are rate-limited per IP (FFL-132, 429).
/// </summary>
[Route("api/checkout/ffl")]
[AllowAnonymous]
public class CheckoutFflController : ApiControllerBase
{
    private const string SessionHeader = "X-Cart-Session";
    private readonly FflSearchRateLimiter _limiter;

    public CheckoutFflController(FflSearchRateLimiter limiter) => _limiter = limiter;

    public sealed record SelectDealerBody(Guid? CartId, string? SessionId, string? DealerKey);

    /// <summary>Radius search for the checkout (radius 15/25/50/100/150 by default).</summary>
    [HttpGet("dealers")]
    public async Task<ActionResult<FflSearchResultDto>> Search([FromQuery] string? zip, [FromQuery] int? radius, CancellationToken ct)
    {
        if (!_limiter.TryConsume(HttpContext.Connection.RemoteIpAddress?.ToString()))
            return TooManySearches();
        return Ok(await Mediator.Send(new SearchFflDealersQuery(zip, radius, FflSearchContext.Checkout), ct));
    }

    [HttpGet("dealers/{key}")]
    public async Task<ActionResult<FflDealerDto>> Dealer(string key, CancellationToken ct)
        => Ok(await Mediator.Send(new GetFflDealerQuery(key), ct));

    /// <summary>The cart's selected (or remembered) dealer and FFL / non-FFL line counts.</summary>
    [HttpGet("selection")]
    public async Task<ActionResult<CheckoutFflStateDto>> Selection([FromQuery] Guid? cartId, [FromQuery] string? sessionId, CancellationToken ct)
        => Ok(await Mediator.Send(new GetCheckoutFflStateQuery(cartId, ResolveSession(sessionId)), ct));

    /// <summary>Select a dealer by key; the address is read from the directory (FFL-52).</summary>
    [HttpPut("selection")]
    public async Task<ActionResult<CheckoutFflStateDto>> Select([FromBody] SelectDealerBody body, CancellationToken ct)
        => Ok(await Mediator.Send(
            new SelectCheckoutFflDealerCommand(body.CartId, ResolveSession(body.SessionId), body.DealerKey ?? string.Empty), ct));

    [HttpDelete("selection")]
    public async Task<IActionResult> Clear([FromQuery] Guid? cartId, [FromQuery] string? sessionId, CancellationToken ct)
    {
        await Mediator.Send(new ClearCheckoutFflDealerCommand(cartId, ResolveSession(sessionId)), ct);
        return NoContent();
    }

    private string? ResolveSession(string? sessionId)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
            return sessionId;
        return Request.Headers.TryGetValue(SessionHeader, out var header) && !string.IsNullOrWhiteSpace(header)
            ? header.ToString()
            : null;
    }

    private ObjectResult TooManySearches() => StatusCode(StatusCodes.Status429TooManyRequests, new
    {
        title = "Too many dealer searches. Please wait a minute and try again.",
        status = StatusCodes.Status429TooManyRequests,
    });
}

/// <summary>Public FFL dealer finder (FFL-100/101) and the storefront FFL configuration.</summary>
[Route("api/ffl")]
[AllowAnonymous]
public class FflPublicController : ApiControllerBase
{
    private readonly FflSearchRateLimiter _limiter;

    public FflPublicController(FflSearchRateLimiter limiter) => _limiter = limiter;

    [HttpGet("config")]
    public async Task<ActionResult<FflPublicConfigDto>> Config(CancellationToken ct)
        => Ok(await Mediator.Send(new GetFflPublicConfigQuery(), ct));

    /// <summary>Radius search for the public finder (radius 5/10/15/25/50 by default).</summary>
    [HttpGet("dealers")]
    public async Task<ActionResult<FflSearchResultDto>> Search([FromQuery] string? zip, [FromQuery] int? radius, CancellationToken ct)
    {
        if (!_limiter.TryConsume(HttpContext.Connection.RemoteIpAddress?.ToString()))
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                title = "Too many dealer searches. Please wait a minute and try again.",
                status = StatusCodes.Status429TooManyRequests,
            });
        return Ok(await Mediator.Send(new SearchFflDealersQuery(zip, radius, FflSearchContext.Public), ct));
    }
}

/// <summary>
/// Admin FFL directory and settings: the dealer list, directory import (ATF licensee list) and geocoding,
/// dealer data (FFL-94/95) and the FFL settings.
/// </summary>
[Route("api/admin/ffl")]
[RequireModule(Modules.Shipping)]
public class AdminFflController : ApiControllerBase
{
    private const long ImportLimitBytes = 104_857_600; // the ATF list is ~15 MB; leave room for a legacy CSV export.

    public sealed record GeocodeBody(int? MaxZips);

    [HttpGet("settings")]
    public async Task<ActionResult<FflSettingsDto>> GetSettings(CancellationToken ct)
        => Ok(await Mediator.Send(new GetFflSettingsQuery(), ct));

    [HttpPut("settings")]
    public async Task<ActionResult<FflSettingsDto>> UpdateSettings([FromBody] UpdateFflSettingsCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    [HttpGet("stats")]
    public async Task<ActionResult<FflDirectoryStatsDto>> Stats(CancellationToken ct)
        => Ok(await Mediator.Send(new GetFflDirectoryStatsQuery(), ct));

    [HttpGet("dealers")]
    public async Task<ActionResult<PaginatedList<FflDealerDto>>> List(
        [FromQuery] string? search, [FromQuery] string? state, [FromQuery] bool? activeOnly,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListFflDealersQuery(search, state, activeOnly, page, pageSize), ct));

    /// <summary>Radius search for the admin dealer pickers (checkout radius list).</summary>
    [HttpGet("dealers/search")]
    public async Task<ActionResult<FflSearchResultDto>> Search([FromQuery] string? zip, [FromQuery] int? radius, CancellationToken ct)
        => Ok(await Mediator.Send(new SearchFflDealersQuery(zip, radius, FflSearchContext.Admin), ct));

    [HttpGet("dealers/{key}")]
    public async Task<ActionResult<FflDealerDto>> Dealer(string key, CancellationToken ct)
        => Ok(await Mediator.Send(new GetFflDealerQuery(key), ct));

    /// <summary>Add one dealer by hand (not yet in the ATF import). Flagged as manual; searchable at once.</summary>
    [HttpPost("dealers")]
    public async Task<ActionResult<FflDealerDto>> CreateDealer([FromBody] CreateFflDealerCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    [HttpPut("dealers/{key}/data")]
    [RequireModule(Modules.Orders)]
    public async Task<ActionResult<FflDealerDto>> UpdateDealerData(string key, [FromBody] UpdateFflDealerDataCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command with { Key = key }, ct));

    /// <summary>Import the ATF licensee list (tab-delimited) or a CSV export of the legacy directory.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(ImportLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportLimitBytes)]
    public async Task<ActionResult<ImportFflDealersResult>> Import(IFormFile file, [FromForm] bool deactivateMissing, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { title = "Choose a file to import.", status = StatusCodes.Status400BadRequest });
        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new ImportFflDealersCommand(stream, deactivateMissing), ct));
    }

    /// <summary>Geocode dealers that have no position yet (by premise ZIP), a batch at a time.</summary>
    [HttpPost("geocode")]
    public async Task<ActionResult<GeocodeFflDealersResult>> Geocode([FromBody] GeocodeBody? body, CancellationToken ct)
        => Ok(await Mediator.Send(new GeocodeFflDealersCommand(body?.MaxZips ?? 500), ct));
}

/// <summary>Select New Shipping Dealer on an order (FFL-92/93).</summary>
[Route("api/admin/orders")]
[RequireModule(Modules.Orders)]
public class AdminOrderFflController : ApiControllerBase
{
    public sealed record ChangeDealerBody(string? DealerKey);

    [HttpPut("{id:guid}/ffl-dealer")]
    public async Task<ActionResult<ChangeOrderFflDealerResult>> ChangeDealer(Guid id, [FromBody] ChangeDealerBody body, CancellationToken ct)
        => Ok(await Mediator.Send(new ChangeOrderFflDealerCommand(id, body.DealerKey ?? string.Empty), ct));
}
