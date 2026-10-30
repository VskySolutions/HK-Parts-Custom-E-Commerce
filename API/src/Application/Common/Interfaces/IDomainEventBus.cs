namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Routes a domain event to the webhook subscriptions that subscribe to it, enqueuing one delivery per
/// matching active subscription (REQ-PLT-003). The dispatch worker then signs and POSTs each delivery.
/// </summary>
public interface IDomainEventBus
{
    /// <summary>Enqueues webhook deliveries for <paramref name="eventType"/> carrying <paramref name="data"/>.</summary>
    Task PublishAsync(string eventType, object data, CancellationToken cancellationToken = default);
}
