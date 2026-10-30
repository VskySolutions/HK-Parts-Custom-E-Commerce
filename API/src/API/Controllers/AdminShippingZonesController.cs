using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ShippingZones;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage geographic shipping zones used by custom shipping-method rates (WO-43).</summary>
[Route("api/admin/shipping-zones")]
[RequireModule(Modules.Shipping)]
public class AdminShippingZonesController : ApiControllerBase
{
    /// <summary>List shipping zones (paged), optionally filtered by name, country and/or enabled state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ShippingZoneDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] string? countryCode = null, [FromQuery] bool? isEnabled = null)
        => Ok(await Mediator.Send(new ListShippingZonesQuery(page, pageSize, search, countryCode, isEnabled)));

    /// <summary>Get a single shipping zone by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShippingZoneDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetShippingZoneQuery(id)));

    /// <summary>Create a new shipping zone.</summary>
    [HttpPost]
    public async Task<ActionResult<ShippingZoneDto>> Create([FromBody] CreateShippingZoneCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing shipping zone (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ShippingZoneDto>> Update(Guid id, [FromBody] UpdateShippingZoneCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a shipping zone.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteShippingZoneCommand(id));
        return NoContent();
    }
}
