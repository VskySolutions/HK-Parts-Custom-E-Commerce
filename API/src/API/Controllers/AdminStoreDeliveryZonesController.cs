using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.DeliveryZones;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage the delivery zones of a store.</summary>
[Route("api/admin/stores/{storeId:guid}/delivery-zones")]
[RequireModule(Modules.Stores)]
public class AdminStoreDeliveryZonesController : ApiControllerBase
{
    /// <summary>List the delivery zones for a store.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeliveryZoneDto>>> List(Guid storeId)
        => Ok(await Mediator.Send(new ListDeliveryZonesQuery(storeId)));

    /// <summary>Create a delivery zone (route storeId wins over any storeId in the body).</summary>
    [HttpPost]
    public async Task<ActionResult<DeliveryZoneDto>> Create(Guid storeId, [FromBody] CreateDeliveryZoneCommand command)
        => Ok(await Mediator.Send(command with { StoreId = storeId }));

    /// <summary>Update a delivery zone (route zoneId wins over any id in the body).</summary>
    [HttpPut("{zoneId:guid}")]
    public async Task<ActionResult<DeliveryZoneDto>> Update(Guid storeId, Guid zoneId, [FromBody] UpdateDeliveryZoneCommand command)
        => Ok(await Mediator.Send(command with { Id = zoneId }));

    /// <summary>Delete a delivery zone.</summary>
    [HttpDelete("{zoneId:guid}")]
    public async Task<IActionResult> Delete(Guid storeId, Guid zoneId)
    {
        await Mediator.Send(new DeleteDeliveryZoneCommand(zoneId));
        return NoContent();
    }
}
