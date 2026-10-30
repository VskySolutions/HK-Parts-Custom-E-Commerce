using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Front door to the payment gateways (REQ-PAY-001). Resolves the concrete
/// <see cref="IPaymentGatewayAdapter"/> for a request's <see cref="PaymentMethodType"/>, applies that
/// gateway's configured <see cref="CaptureMode"/>, and returns a normalized <see cref="PaymentResult"/>
/// so callers stay provider-agnostic.
/// </summary>
public interface IPaymentGatewayRouter
{
    /// <summary>Authorizes a payment, resolving the gateway + its capture mode from configuration.</summary>
    Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>Captures an authorized payment (full or partial) via its originating gateway.</summary>
    Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default);

    /// <summary>Refunds a captured payment (full or partial) via its originating gateway (REQ-PAY-003).</summary>
    Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default);

    /// <summary>Verifies a redirect gateway's off-site payment on return (Stripe Checkout); Captured when paid.</summary>
    Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default);

    /// <summary>
    /// Verifies a client-completed gateway's on-site payment (Razorpay Checkout) from the tokens the widget
    /// returned, captures it, and reports Captured when it succeeds.
    /// </summary>
    Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default);

    /// <summary>
    /// Auto-captures the order's authorized-but-uncaptured payment, e.g. when the order ships under an
    /// authorize-only gateway (AC-PAY-002.3). No-op when there is nothing to capture.
    /// </summary>
    Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// The payment methods currently offered at checkout: those that are enabled and have their
    /// credentials configured (manual methods need no credentials) — AC-PAY-001.1. Each carries the
    /// active credential's live/sandbox mode (null for manual methods).
    /// </summary>
    Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default);
}
