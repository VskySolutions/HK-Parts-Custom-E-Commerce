using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Purges <b>provisional</b> orders — ones created only to hold a redirect (Stripe Checkout) payment session
/// that the buyer never completed (cancelled or abandoned on the gateway). Such orders carry a
/// <c>SourceCartId</c> and stay <see cref="PaymentStatus.Pending"/>; once payment is captured they are no
/// longer provisional and are left untouched. Only orders older than a configurable threshold are removed,
/// so an in-flight checkout is never deleted mid-redirect. Each order's line items, status history, payment
/// records and its order-owned shipping address are removed with it.
/// <para>Threshold: setting <c>orders.abandoned.threshold-minutes</c> (default 60). Runs hourly.</para>
/// </summary>
public class AbandonedOrderCleanupWorker : IScheduledTask
{
    private const int BatchSize = 200;

    public string Name => "AbandonedOrderCleanupWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(60);
    public string? IntervalSettingKey => "tasks.abandoned-order.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var settings = services.GetRequiredService<ISettingsService>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var thresholdMinutes = await settings.GetAsync<int>("orders.abandoned.threshold-minutes", cancellationToken);
        if (thresholdMinutes <= 0)
            thresholdMinutes = 60;

        var cutoff = DateTime.UtcNow.AddMinutes(-thresholdMinutes);

        // Provisional = created for a redirect payment (has SourceCartId), still unpaid (PaymentStatus.Pending),
        // and older than the threshold. Batched so a backlog is drained over successive runs.
        var abandonedIds = await db.Orders
            .Where(o => o.SourceCartId != null
                        && o.PaymentStatus == PaymentStatus.Pending
                        && o.PlacedOnUtc < cutoff)
            .OrderBy(o => o.PlacedOnUtc)
            .Select(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (abandonedIds.Count == 0)
        {
            logger.LogInformation("{Task} ran; no abandoned orders to purge.", Name);
            return;
        }

        // The shipping addresses these orders own (each checkout snapshots a fresh Address), deleted last —
        // Order is the FK dependent of Address, so the order rows must go first.
        var addressIds = await db.Orders
            .Where(o => abandonedIds.Contains(o.Id) && o.ShippingAddressId != null)
            .Select(o => o.ShippingAddressId!.Value)
            .ToListAsync(cancellationToken);

        // Delete dependents before the orders, then the orphaned addresses — FK-safe regardless of cascade config.
        await db.OrderStatusHistory.Where(h => abandonedIds.Contains(h.OrderId)).ExecuteDeleteAsync(cancellationToken);
        await db.OrderLineItems.Where(l => abandonedIds.Contains(l.OrderId)).ExecuteDeleteAsync(cancellationToken);
        await db.PaymentRecords.Where(p => abandonedIds.Contains(p.OrderId)).ExecuteDeleteAsync(cancellationToken);
        var removed = await db.Orders.Where(o => abandonedIds.Contains(o.Id)).ExecuteDeleteAsync(cancellationToken);
        if (addressIds.Count > 0)
            await db.Addresses.Where(a => addressIds.Contains(a.Id)).ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("{Task} ran; purged {Count} abandoned order(s) older than {Minutes} min.", Name, removed, thresholdMinutes);
    }
}
