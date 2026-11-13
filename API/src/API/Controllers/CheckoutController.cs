using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// One-page checkout surface (REQ-CHK-003). Public by design — guests check out too, gated per store by
/// the guest-ordering policy (a placement against a store that forbids guest ordering returns 401 for an
/// unauthenticated buyer). <c>quote</c> is a read-only price preview; <c>place</c> finalizes the order and
/// authorizes payment. Shares the <c>api/checkout</c> route prefix with the shipping-rates endpoint via
/// distinct action routes.
/// </summary>
[Route("api/checkout")]
[AllowAnonymous]
public class CheckoutController : ApiControllerBase
{
    /// <summary>Price the caller's cart for a delivery address (subtotal, discounts, shipping, tax, total).</summary>
    [HttpPost("quote")]
    public async Task<ActionResult<CheckoutQuote>> Quote(
        [FromBody] CheckoutQuoteRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new QuoteCheckoutQuery(request), cancellationToken));

    /// <summary>Place the order: creates the order, authorizes payment, and (on success) confirms checkout.</summary>
    [HttpPost("place")]
    public async Task<ActionResult<CheckoutResult>> Place(
        [FromBody] PlaceCheckoutRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PlaceCheckoutCommand(request), cancellationToken));

    /// <summary>
    /// Confirm the "Pay with Gift Certificate" page: places the order paid entirely by the cart's gift
    /// certificate. 409 when the certificate does not cover the full total; 422 when it is no longer valid.
    /// </summary>
    [HttpPost("pay-with-gift-certificate")]
    public async Task<ActionResult<CheckoutResult>> PayWithGiftCertificate(
        [FromBody] PlaceCheckoutRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PlaceCheckoutCommand(request with
        {
            PayWithGiftCertificate = true,
            PaymentMethod = Domain.Enums.PaymentMethodType.GiftCertificate,
        }), cancellationToken));

    /// <summary>
    /// Confirm the "No payment required" page (PY-40..42): the order total is 0.00 (e.g. a 100 % coupon).
    /// Runs the same pre-payment checks as a card payment. 409 TOTAL_CHANGED when the total is no longer 0.00.
    /// </summary>
    [HttpPost("pay-none")]
    public async Task<ActionResult<CheckoutResult>> PayNone(
        [FromBody] PlaceCheckoutRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new PlaceCheckoutCommand(request with
        {
            PayWithGiftCertificate = false,
            PaymentMethod = Domain.Enums.PaymentMethodType.NoPaymentRequired,
            PaymentToken = null,
        }), cancellationToken));

    /// <summary>Release the cart's low-stock checkout lock (CK-23): the storefront countdown ran out.</summary>
    [HttpPost("lock/release")]
    public async Task<IActionResult> ReleaseLock([FromBody] CheckoutCartRef request, CancellationToken cancellationToken)
    {
        await Mediator.Send(new ReleaseCheckoutLockCommand(request), cancellationToken);
        return NoContent();
    }

    /// <summary>Submit an inquiry (quote request) — no payment is taken (REQ-INQ-001).</summary>
    [HttpPost("inquiry")]
    public async Task<ActionResult<InquiryResult>> Inquiry(
        [FromBody] SubmitInquiryRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new SubmitInquiryCommand(request), cancellationToken));

    /// <summary>Confirm a redirect payment on return (Stripe Checkout): verify the session and finalize on success.</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<CheckoutResult>> Confirm(
        [FromBody] CheckoutOrderRef request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new ConfirmCheckoutCommand(request.OrderId), cancellationToken));

    /// <summary>Confirm an on-site widget payment (Razorpay Checkout): verify the returned tokens and capture on success.</summary>
    [HttpPost("confirm-client-payment")]
    public async Task<ActionResult<CheckoutResult>> ConfirmClientPayment(
        [FromBody] ConfirmClientPaymentRequest request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(
            new ConfirmClientPaymentCommand(request.OrderId, request.GatewayData ?? new Dictionary<string, string>()),
            cancellationToken));

    /// <summary>Re-open a payment session for a still-pending order (retry after a cancelled redirect payment).</summary>
    [HttpPost("retry-payment")]
    public async Task<ActionResult<CheckoutResult>> RetryPayment(
        [FromBody] CheckoutOrderRef request, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new RetryCheckoutPaymentCommand(request.OrderId), cancellationToken));
}
