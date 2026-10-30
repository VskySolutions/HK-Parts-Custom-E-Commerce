using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Payments;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin management of the order lifecycle state machine (WO-45): advance an order through its
/// fulfilment states and inspect its immutable status history. Routes are distinct from
/// <see cref="AdminOrdersController"/>, so both controllers share the <c>api/admin/orders</c> prefix.
/// </summary>
[Route("api/admin/orders")]
[RequireModule(Modules.Orders)]
public class AdminOrderLifecycleController : ApiControllerBase
{
    /// <summary>
    /// Advance an order to the given lifecycle status (route id wins). Shipping requires a tracking
    /// number and carrier in the body; invalid transitions return 409 Conflict.
    /// </summary>
    [HttpPut("{id:guid}/advance")]
    public async Task<ActionResult<OrderDto>> Advance(Guid id, [FromBody] AdvanceOrderStatusCommand command)
        => Ok(await Mediator.Send(command with { OrderId = id }));

    /// <summary>Get an order's full status-transition history, oldest first.</summary>
    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<List<OrderStatusHistoryDto>>> History(Guid id)
        => Ok(await Mediator.Send(new GetOrderStatusHistoryQuery(id)));

    /// <summary>Get the order's payment records (authorize → capture → refund), newest first.</summary>
    [HttpGet("{id:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> Payments(Guid id)
        => Ok(await Mediator.Send(new GetOrderPaymentsQuery(id)));
}
