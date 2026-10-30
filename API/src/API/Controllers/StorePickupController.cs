using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Store-manager pickup fulfilment (AC-SHP-004.3): mark a pickup order ready for collection.</summary>
[Route("api/store/pickup")]
[Authorize]
public class StorePickupController : ApiControllerBase
{
    /// <summary>Mark a pickup order (assigned to the caller's store) ready for collection; notifies the buyer.</summary>
    [HttpPut("orders/{id:guid}/ready")]
    public async Task<ActionResult<OrderDto>> MarkReady(Guid id)
        => Ok(await Mediator.Send(new MarkOrderReadyForPickupCommand(id)));
}
