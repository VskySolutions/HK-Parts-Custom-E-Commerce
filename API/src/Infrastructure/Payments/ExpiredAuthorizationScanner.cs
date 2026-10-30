using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments;

/// <summary>
/// Detects authorization holds that expired before capture (WO-34, AC-PAY-002.4). For each such payment
/// it raises an <see cref="IAdminAlertService"/> alert and voids the lapsed hold locally (the gateway
/// hold has already released), mirroring the void onto the order. Voiding also makes the scan idempotent
/// so a lapsed hold is not re-alerted on the next run.
/// </summary>
public class ExpiredAuthorizationScanner : IExpiredAuthorizationScanner
{
    private readonly IApplicationDbContext _db;
    private readonly IAdminAlertService _alerts;
    private readonly IDateTimeProvider _clock;

    public ExpiredAuthorizationScanner(IApplicationDbContext db, IAdminAlertService alerts, IDateTimeProvider clock)
    {
        _db = db;
        _alerts = alerts;
        _clock = clock;
    }

    public async Task<int> ScanAsync(CancellationToken ct = default)
    {
        var now = _clock.UtcNow;

        var expired = await _db.PaymentRecords
            .Where(p => p.Status == PaymentStatus.Authorized
                        && p.AuthorizationExpiresUtc != null
                        && p.AuthorizationExpiresUtc < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return 0;

        var orderIds = expired.Select(p => p.OrderId).Distinct().ToList();
        var orders = await _db.Orders
            .Where(o => orderIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, ct);

        foreach (var payment in expired)
        {
            payment.Status = PaymentStatus.Voided;
            payment.ErrorMessage = $"Authorization expired at {payment.AuthorizationExpiresUtc:u} before capture.";

            if (orders.TryGetValue(payment.OrderId, out var order))
                order.PaymentStatus = PaymentStatus.Voided;

            await _alerts.RaiseAsync(
                "PaymentAuthorizationExpired",
                $"Payment authorization expired for order {payment.OrderId}",
                $"The {payment.Method} authorization of {payment.Amount:0.00} {payment.CurrencyCode} " +
                $"(payment {payment.Id}) expired before capture and was voided. Review the order for re-charge or cancellation.",
                "Warning",
                "ExpiredAuthorizationScanner",
                ct);
        }

        await _db.SaveChangesAsync(ct);
        return expired.Count;
    }
}
