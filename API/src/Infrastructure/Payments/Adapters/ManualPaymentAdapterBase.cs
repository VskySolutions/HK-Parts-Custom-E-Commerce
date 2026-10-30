using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Shared behavior for manual/offline payment methods (WO-34, AC-PAY-001.5): no external gateway call.
/// Authorization leaves the order awaiting out-of-band payment; capture and refund are admin actions that
/// simply advance the local record. A deterministic local reference is generated from the order id.
/// </summary>
public abstract class ManualPaymentAdapterBase : IPaymentGatewayAdapter
{
    public abstract PaymentMethodType Method { get; }

    /// <summary>Prefix for the generated local reference (e.g. "COD", "BANK").</summary>
    protected abstract string ReferencePrefix { get; }

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
    {
        var reference = BuildReference(req.OrderId);
        // Funds settle out-of-band, so the order simply awaits payment — no hold, no capture yet.
        return Task.FromResult(new PaymentResult(
            true, PaymentStatus.AwaitingPayment, AuthorizationId: reference, GatewayReference: reference));
    }

    public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
    {
        var reference = payment.GatewayReference ?? payment.AuthorizationId ?? BuildReference(payment.OrderId);
        return Task.FromResult(PaymentResult.Captured(reference, amount, reference, reference));
    }

    public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
    {
        var reference = payment.GatewayReference ?? payment.AuthorizationId ?? BuildReference(payment.OrderId);
        return Task.FromResult(new PaymentResult(
            true, PaymentStatus.Refunded, TransactionId: reference, GatewayReference: reference, CapturedAmount: amount));
    }

    /// <summary>Manual methods are not redirect-based — nothing to verify on return.</summary>
    public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct)
        => Task.FromResult(PaymentResult.Failed("This gateway does not use a redirect flow.", PaymentStatus.Pending));

    /// <summary>Manual methods have no on-site widget — nothing to verify.</summary>
    public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct)
        => Task.FromResult(PaymentResult.Failed("This gateway does not use a client-side payment flow.", PaymentStatus.Pending));

    private string BuildReference(Guid orderId) => $"{ReferencePrefix}-{orderId:N}";
}
