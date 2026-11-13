using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// A single payment provider's implementation of the authorize → capture → refund contract
/// (REQ-PAY-001/002/003). Each adapter owns one <see cref="PaymentMethodType"/>; the
/// <see cref="IPaymentGatewayRouter"/> selects the right one at runtime and normalizes the outcome to
/// <see cref="PaymentResult"/>. Implementations resolve their own credentials from the Credential
/// Vault and must never throw for a provider-side failure — they return a failed
/// <see cref="PaymentResult"/> instead.
/// </summary>
public interface IPaymentGatewayAdapter
{
    /// <summary>The payment method this adapter handles.</summary>
    PaymentMethodType Method { get; }

    /// <summary>
    /// Authorizes (and, when <paramref name="mode"/> is <see cref="CaptureMode.AuthorizeAndCapture"/>,
    /// captures) the requested amount. Honors the gateway's configured capture mode (REQ-PAY-002).
    /// </summary>
    Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct);

    /// <summary>Captures a previously authorized payment, in full or part (AC-PAY-002.x).</summary>
    Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct);

    /// <summary>Refunds a captured payment, in full or part (REQ-PAY-003).</summary>
    Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct);

    /// <summary>
    /// For redirect gateways (Stripe Checkout): verifies the off-site payment on the buyer's return and
    /// reports it Captured when paid. Non-redirect gateways return a non-captured result (base default).
    /// </summary>
    Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct);

    /// <summary>
    /// For client-completed gateways (Razorpay Checkout): verifies the tokens the on-site widget returned
    /// (<paramref name="data"/> — e.g. payment id + signature), captures the payment, and reports it
    /// Captured when it succeeds. Non-widget gateways return a non-captured result (base default).
    /// </summary>
    Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct);

    /// <summary>
    /// Reverses the whole outstanding amount when an order is cancelled: voids an unsettled transaction, refunds
    /// a settled one (Authorize.Net AN-24). Returns <c>null</c> when the gateway has no automatic reversal, in
    /// which case the cancellation leaves the payment for the admin to refund by hand (the previous behaviour).
    /// </summary>
    Task<PaymentResult?> ReverseAsync(PaymentRecord payment, CancellationToken ct)
        => Task.FromResult<PaymentResult?>(null);

    /// <summary>
    /// Looks the payment up at the gateway and reports its current state (e.g. a held-for-review transaction the
    /// merchant has since approved → Captured / Authorized, or declined → Failed). <c>null</c> when unsupported.
    /// </summary>
    Task<PaymentResult?> GetStatusAsync(PaymentRecord payment, CancellationToken ct)
        => Task.FromResult<PaymentResult?>(null);
}
