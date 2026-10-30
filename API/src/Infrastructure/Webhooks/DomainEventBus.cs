using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Webhooks;

/// <summary>
/// Enqueues a webhook delivery per active subscription that subscribes to the event (REQ-PLT-003). One
/// failing endpoint never blocks another — deliveries are independent rows dispatched by the worker.
/// </summary>
public class DomainEventBus : IDomainEventBus
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public DomainEventBus(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task PublishAsync(string eventType, object data, CancellationToken cancellationToken = default)
    {
        var subscriptionIds = await _db.WebhookSubscriptions
            .Where(s => s.IsActive && s.Events.Any(e => e.EventType == eventType))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (subscriptionIds.Count == 0)
            return;

        var now = _clock.UtcNow;
        var payloadJson = JsonSerializer.Serialize(data);

        foreach (var subscriptionId in subscriptionIds)
        {
            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SubscriptionId = subscriptionId,
                EventType = eventType,
                PayloadJson = payloadJson,
                OccurredAtUtc = now,
                Status = WebhookDeliveryStatus.Pending,
                NextAttemptOnUtc = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
