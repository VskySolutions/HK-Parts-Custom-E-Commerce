using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Tests.Common;

/// <summary>Test doubles for the order/inventory handler dependencies (WO stock lifecycle).</summary>
internal sealed class FakeAdminAlertService : IAdminAlertService
{
    public int Raised { get; private set; }

    public Task RaiseAsync(string alertType, string title, string? message = null, string severity = "Warning",
        string? source = null, CancellationToken cancellationToken = default)
    {
        Raised++;
        return Task.CompletedTask;
    }
}

/// <summary>No-op MediatR publisher — the inventory service publishes replenishment events we don't assert on.</summary>
internal sealed class FakeNoopPublisher : IPublisher
{
    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification => Task.CompletedTask;
}

/// <summary>Payment router stub: only <see cref="CaptureForOrderAsync"/> is exercised (a no-op on cancellation).</summary>
internal sealed class FakePaymentRouter : IPaymentGatewayRouter
{
    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default)
        => throw new NotSupportedException();
}

/// <summary>
/// ISender stub for RefundOrder tests: returns a Refunded <see cref="PaymentDto"/> for the inner
/// <see cref="RefundPaymentCommand"/> without touching a real gateway, and records how many refunds ran.
/// </summary>
internal sealed class FakeRefundSender : ISender
{
    public int RefundCalls { get; private set; }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (request is RefundPaymentCommand)
        {
            RefundCalls++;
            return Task.FromResult((TResponse)(object)new PaymentDto { Status = PaymentStatus.Refunded });
        }

        throw new NotSupportedException($"Unexpected request {request.GetType().Name}");
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest => Task.CompletedTask;

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>No-op loyalty ledger (WO-27) for handler tests that do not exercise reward points.</summary>
internal sealed class FakeRewardPointsService : IRewardPointsService
{
    public Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<LoyaltyConfig> GetConfigAsync(CancellationToken cancellationToken = default) => Task.FromResult(new LoyaltyConfig(false, 1m, 100m));
    public Task<int> GetBalanceAsync(Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    public Task CreditForOrderAsync(Guid customerId, decimal orderTotal, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public decimal ToDiscountValue(int points, decimal redeemRate) => 0m;
    public Task RedeemAsync(Guid customerId, int points, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task DeductForRefundAsync(Guid customerId, Guid orderId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
