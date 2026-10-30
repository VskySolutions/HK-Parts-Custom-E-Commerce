using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Polls active shipments' carriers for tracking updates (REQ-SHP-002): appends a tracking checkpoint,
/// updates the shipment status, and — on carrier-confirmed delivery — transitions the order to Delivered
/// and notifies the buyer (AC-SHP-002.3/002.4). Carriers that are unconfigured return no update and are
/// skipped. (Currency-rate refresh, formerly here, now lives in CurrencyRateRefreshWorker.)
/// </summary>
public class TrackingSyncWorker : IScheduledTask
{
    private const int BatchSize = 100;

    public string Name => "TrackingSyncWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(180);
    public string? IntervalSettingKey => "tasks.tracking-sync.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var carriers = services.GetServices<ICarrierClient>().ToList();
        var emails = services.GetRequiredService<IEmailEnqueuer>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var now = DateTime.UtcNow;
        var active = await db.Shipments
            .Where(s => s.Status != ShipmentStatus.Delivered
                        && s.Status != ShipmentStatus.Cancelled
                        && s.TrackingNumber != null
                        && s.Carrier != null)
            .OrderBy(s => s.LastPolledOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (active.Count == 0)
        {
            logger.LogInformation("{Task} ran; no active shipments to poll.", Name);
            return;
        }

        var polled = 0;
        var delivered = 0;
        foreach (var shipment in active)
        {
            var carrier = carriers.FirstOrDefault(c => c.CarrierName.Equals(shipment.Carrier, StringComparison.OrdinalIgnoreCase));
            if (carrier is null)
                continue;

            CarrierTrackingResult? result;
            try
            {
                result = await carrier.GetTrackingStatusAsync(shipment.TrackingNumber!, cancellationToken);
            }
            catch
            {
                continue; // a carrier failure never blocks the rest of the batch
            }

            shipment.LastPolledOnUtc = now;
            polled++;
            if (result is null)
                continue;

            db.ShipmentTrackingEvents.Add(new ShipmentTracking
            {
                ShipmentId = shipment.Id,
                RawStatus = result.RawStatus,
                NormalizedStatus = result.NormalizedStatus,
                Description = result.Description,
                Location = result.Location,
                CheckpointOnUtc = result.CheckpointOnUtc,
                RecordedOnUtc = now,
            });
            shipment.Status = result.NormalizedStatus;

            if (result.NormalizedStatus == ShipmentStatus.Delivered)
            {
                shipment.DeliveredOnUtc = now;
                await MarkOrderDeliveredAsync(db, emails, shipment.OrderId, now, cancellationToken);
                delivered++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Task} ran; polled {Polled} shipment(s), {Delivered} delivered.", Name, polled, delivered);
    }

    private static async Task MarkOrderDeliveredAsync(AppDbContext db, IEmailEnqueuer emails, Guid orderId, DateTime now, CancellationToken ct)
    {
        var order = await db.Orders.Include(o => o.ShippingAddress).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.Status == OrderStatus.Delivered)
            return;

        // Only advance from Shipped (the lifecycle's pre-delivery state).
        if (order.Status != OrderStatus.Shipped)
            return;

        var from = order.Status;
        order.Status = OrderStatus.Delivered;
        order.DeliveredOnUtc = now;
        db.OrderStatusHistory.Add(new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = from,
            ToStatus = OrderStatus.Delivered,
            ChangedOnUtc = now,
        });

        if (!string.IsNullOrWhiteSpace(order.ContactEmail))
        {
            await emails.EnqueueAsync(
                "OrderDelivered",
                order.ContactEmail!,
                order.ContactName,
                $"Your order {order.OrderNumber} has been delivered",
                $"Hi {order.ContactName},\n\nYour order {order.OrderNumber} has been delivered.\n\n" +
                "We hope you enjoy your purchase — thank you for shopping with us.",
                cancellationToken: ct);
        }
    }
}
