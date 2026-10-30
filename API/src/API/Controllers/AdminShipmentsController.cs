using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Application.Features.Shipments;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Partial shipments + partial refunds for an order (REQ-ORD-002). Shares the <c>api/admin/orders</c>
/// prefix with the lifecycle/documents controllers (distinct action templates).
/// </summary>
[Route("api/admin/orders")]
[RequireModule(Modules.Orders)]
public class AdminShipmentsController : ApiControllerBase
{
    /// <summary>Create a shipment for selected order lines + quantities.</summary>
    [HttpPost("{id:guid}/shipments")]
    public async Task<ActionResult<ShipmentDto>> CreateShipment(Guid id, [FromBody] CreateShipmentCommand command)
        => Ok(await Mediator.Send(command with { OrderId = id }));

    /// <summary>List the order's shipments.</summary>
    [HttpGet("{id:guid}/shipments")]
    public async Task<ActionResult<List<ShipmentDto>>> ListShipments(Guid id)
        => Ok(await Mediator.Send(new ListOrderShipmentsQuery(id)));

    /// <summary>Refund the order by selected lines, explicit amount, or in full (through the original gateway).</summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<PaymentDto>> Refund(Guid id, [FromBody] RefundOrderCommand command)
        => Ok(await Mediator.Send(command with { OrderId = id }));

    /// <summary>Generate a carrier label for a shipment (stores the tracking number + label URL).</summary>
    [HttpPost("shipments/{shipmentId:guid}/label")]
    public async Task<ActionResult<ShipmentDto>> GenerateLabel(Guid shipmentId)
        => Ok(await Mediator.Send(new GenerateShipmentLabelCommand(shipmentId)));

    /// <summary>Latest carrier tracking checkpoints across the order's shipments.</summary>
    [HttpGet("{id:guid}/tracking")]
    public async Task<ActionResult<List<ShipmentTrackingDto>>> Tracking(Guid id)
        => Ok(await Mediator.Send(new GetOrderTrackingQuery(id)));
}
