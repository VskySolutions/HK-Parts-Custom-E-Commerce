using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ShipStation;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>ShipStation settings and call log (docs/shipstation-verification-requirements.md §5.1, §6.1).</summary>
[Route("api/admin/shipstation")]
[RequireModule(Modules.Shipping)]
public class AdminShipStationController : ApiControllerBase
{
    /// <summary>The settings (password write-only), the feed URL and the last export / shipnotify calls.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<ShipStationSettingsDto>> Get(CancellationToken ct)
        => Ok(await Mediator.Send(new GetShipStationSettingsQuery(FeedUrl()), ct));

    /// <summary>Save the settings. A blank password keeps the stored one.</summary>
    [HttpPut("settings")]
    public async Task<ActionResult<ShipStationSettingsDto>> Update([FromBody] UpdateShipStationSettingsCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command with { FeedUrl = FeedUrl() }, ct));

    /// <summary>The ShipStation call log, newest first.</summary>
    [HttpGet("activity")]
    public async Task<ActionResult<PaginatedList<ShipStationLogDto>>> Activity(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? action,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListShipStationActivityQuery(from, to, action, page, pageSize), ct));

    /// <summary>The URL to paste into ShipStation (SS-07): this API's host + /shipstation, https except on localhost (plain-HTTP dev).</summary>
    private string FeedUrl() =>
        $"{(Request.Host.Host == "localhost" ? Request.Scheme : "https")}://{Request.Host}/shipstation";
}
