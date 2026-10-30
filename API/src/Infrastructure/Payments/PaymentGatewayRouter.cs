using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments;

/// <summary>
/// Payment gateway router (WO-32). Resolves the concrete <see cref="IPaymentGatewayAdapter"/> for a
/// method from the injected adapter set, applies that gateway's configured <see cref="CaptureMode"/>
/// (settings key <c>payments:{method}:captureMode</c>, default
/// <see cref="CaptureMode.AuthorizeAndCapture"/>), and returns a normalized <see cref="PaymentResult"/>.
/// Adapters resolve their own credentials from the Credential Vault; the router uses the vault only to
/// decide which methods are available at checkout (AC-PAY-001.1).
/// </summary>
public class PaymentGatewayRouter : IPaymentGatewayRouter
{
    private readonly IEnumerable<IPaymentGatewayAdapter> _adapters;
    private readonly ISettingsService _settings;
    private readonly ICredentialVault _vault;
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public PaymentGatewayRouter(
        IEnumerable<IPaymentGatewayAdapter> adapters,
        ISettingsService settings,
        ICredentialVault vault,
        IApplicationDbContext db,
        IDateTimeProvider clock)
    {
        _adapters = adapters;
        _settings = settings;
        _vault = vault;
        _db = db;
        _clock = clock;
    }

    public async Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        var adapter = Resolve(request.Method);
        var mode = await GetCaptureModeAsync(request.Method, ct);
        return await adapter.AuthorizeAsync(request, mode, ct);
    }

    public Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
        => Resolve(payment.Method).CaptureAsync(payment, amount, ct);

    public Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct = default)
        => Resolve(payment.Method).RefundAsync(payment, amount, ct);

    public Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct = default)
        => Resolve(payment.Method).VerifyRedirectAsync(payment, ct);

    public Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct = default)
        => Resolve(payment.Method).VerifyClientPaymentAsync(payment, data, ct);

    public async Task CaptureForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        // Auto-capture the order's authorized-but-uncaptured payment when it ships/fulfils (AC-PAY-002.3).
        var payment = await _db.PaymentRecords
            .FirstOrDefaultAsync(p => p.OrderId == orderId && p.Status == PaymentStatus.Authorized, ct);
        if (payment is null)
            return;

        var outstanding = payment.Amount - payment.RefundedAmount;
        if (outstanding <= 0)
            return;

        var result = await Resolve(payment.Method).CaptureAsync(payment, outstanding, ct);

        if (result.Success)
        {
            payment.Status = PaymentStatus.Captured;
            payment.TransactionId = result.TransactionId ?? payment.TransactionId;
            payment.GatewayReference = result.GatewayReference ?? payment.GatewayReference;
            payment.CapturedOnUtc = _clock.UtcNow;
            payment.ErrorMessage = null;

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
            if (order is not null)
                order.PaymentStatus = PaymentStatus.Captured;
        }
        else
        {
            // Leave the authorization intact so it can be retried or expire into review; record why.
            payment.ErrorMessage = result.ErrorMessage;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PaymentMethodAvailability>> AvailableMethodsAsync(CancellationToken ct = default)
    {
        var available = new List<PaymentMethodAvailability>();

        foreach (var adapter in _adapters)
        {
            var method = adapter.Method;

            // A method must be explicitly enabled (default on) to be offered.
            var enabled = await _settings.GetValueAsync(PaymentGatewayDefaults.EnabledKey(method), ct);
            if (string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase))
                continue;

            // Manual methods need no credentials and have no sandbox/live environment (IsProduction = null).
            if (PaymentGatewayDefaults.IsManual(method))
            {
                available.Add(new PaymentMethodAvailability(method, null));
                continue;
            }

            var serviceType = PaymentGatewayDefaults.CredentialServiceType(method);
            if (serviceType is null)
                continue;

            // A gateway is offered only when its active credential is configured; that row also tells us
            // whether it is a live or sandbox account and carries the optional per-transaction fee.
            var credential = await _vault.GetResolvedCredentialAsync(serviceType, ct);
            if (credential is not null)
                available.Add(new PaymentMethodAvailability(method, credential.IsProduction, credential.TransactionFeePercent ?? 0m));
        }

        return available;
    }

    private IPaymentGatewayAdapter Resolve(PaymentMethodType method)
        => _adapters.FirstOrDefault(a => a.Method == method)
           ?? throw new NotSupportedException($"No payment gateway adapter is registered for method '{method}'.");

    private async Task<CaptureMode> GetCaptureModeAsync(PaymentMethodType method, CancellationToken ct)
    {
        var raw = await _settings.GetValueAsync(PaymentGatewayDefaults.CaptureModeKey(method), ct);
        return Enum.TryParse<CaptureMode>(raw, ignoreCase: true, out var mode)
            ? mode
            : CaptureMode.AuthorizeAndCapture;
    }
}
