using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's own order history and order detail (WO-45 buyer view).</summary>
[Route("api/customer/orders")]
[Authorize]
public class CustomerOrdersController : ApiControllerBase
{
    /// <summary>List the current customer's own orders (paged), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<OrderSummaryDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListMyOrdersQuery(page, pageSize)));

    /// <summary>Get one of the current customer's own orders including its line items.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetMyOrderQuery(id)));

    /// <summary>The current customer's own order status timeline (oldest transition first).</summary>
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<List<OrderStatusHistoryDto>>> Timeline(Guid id)
        => Ok(await Mediator.Send(new GetMyOrderTimelineQuery(id)));

    /// <summary>Download the current customer's own order invoice as a PDF.</summary>
    [HttpGet("{id:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid id)
    {
        // Authorize first: GetMyOrderQuery throws NotFound unless the order belongs to the signed-in
        // customer, so a buyer can only ever download their own invoice.
        await Mediator.Send(new GetMyOrderQuery(id));
        var bytes = await Mediator.Send(new GetInvoicePdfQuery(id));
        return File(bytes, "application/pdf", $"invoice-{id}.pdf");
    }
}
