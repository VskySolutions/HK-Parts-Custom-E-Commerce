using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The authenticated store manager's own store: order queue (accept / reject / fulfilment) and stock
/// updates. Every action is scoped to the caller's assigned store (WO-52, REQ-STR-002/004).
/// </summary>
[Route("api/store/order-queue")]
[Authorize]
public class StoreOrderQueueController : ApiControllerBase
{
    /// <summary>List the manager's store queue, newest first, optionally filtered by status.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<OrderSummaryDto>>> Queue(
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListStoreQueueQuery(status, page, pageSize)));

    /// <summary>Accept a routed order in the manager's store.</summary>
    [HttpPut("orders/{id:guid}/accept")]
    public async Task<ActionResult<OrderDto>> Accept(Guid id)
        => Ok(await Mediator.Send(new AcceptOrderCommand(id)));

    /// <summary>Reject an order in the manager's store (re-routes to the next eligible store).</summary>
    [HttpPut("orders/{id:guid}/reject")]
    public async Task<ActionResult<OrderDto>> Reject(Guid id)
        => Ok(await Mediator.Send(new RejectOrderCommand(id)));

    /// <summary>Advance an order through fulfilment (Preparing / Shipped / Delivered); route id wins.</summary>
    [HttpPut("orders/{id:guid}/fulfilment")]
    public async Task<ActionResult<OrderDto>> Fulfilment(Guid id, [FromBody] UpdateFulfilmentStatusCommand command)
        => Ok(await Mediator.Send(command with { OrderId = id }));

    /// <summary>Set a stock level at the manager's own store.</summary>
    [HttpPut("inventory")]
    public async Task<IActionResult> UpdateInventory([FromBody] UpdateStoreInventoryCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }
}
