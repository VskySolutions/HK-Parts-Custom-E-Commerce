using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public order intake. This is a minimal endpoint standing in for the future checkout flow
/// (WO-50 checkout orchestrator): it accepts a delivery address + line items, snapshots pricing,
/// routes the order to a fulfilment store and reserves stock at the assigned store.
/// </summary>
[Route("api/orders")]
[AllowAnonymous]
public class OrdersController : ApiControllerBase
{
    /// <summary>Place an order (guest or authenticated).</summary>
    [HttpPost]
    public async Task<ActionResult<OrderDto>> Place([FromBody] PlaceOrderCommand command)
        => Ok(await Mediator.Send(command));
}
