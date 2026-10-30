using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Payments;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Payment intake + lifecycle (WO-32/33/34). Submission is public (checkout); capture, refund and the
/// payment history are admin operations gated by the Payments module.
/// </summary>
[Route("api/payments")]
public class PaymentController : ApiControllerBase
{
    /// <summary>Submit a payment for an order (authorizes, and captures under authorize-and-capture gateways).</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<PaymentDto>> Submit([FromBody] SubmitPaymentCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Capture an authorized payment (full, or partial via the optional body amount).</summary>
    [HttpPost("{id:guid}/capture")]
    [RequireModule(Modules.Payments)]
    public async Task<ActionResult<PaymentDto>> Capture(Guid id, [FromBody] CapturePaymentRequest? body = null)
        => Ok(await Mediator.Send(new CapturePaymentCommand(id, body?.Amount)));

    /// <summary>Refund a captured payment (full, or partial via the optional body amount).</summary>
    [HttpPost("{id:guid}/refund")]
    [RequireModule(Modules.Payments)]
    public async Task<ActionResult<PaymentDto>> Refund(Guid id, [FromBody] RefundPaymentRequest? body = null)
        => Ok(await Mediator.Send(new RefundPaymentCommand(id, body?.Amount, body?.Reason)));

    /// <summary>List all payment records for an order (newest first).</summary>
    [HttpGet("order/{orderId:guid}")]
    [RequireModule(Modules.Payments)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetForOrder(Guid orderId)
        => Ok(await Mediator.Send(new GetOrderPaymentsQuery(orderId)));
}
