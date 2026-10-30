using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An admin-registered webhook endpoint (REQ-PLT-003). Subscribed domain events are delivered as signed
/// HTTP POSTs; the shared <see cref="Secret"/> signs each payload (HMAC-SHA256) so the recipient can
/// verify authenticity.
/// </summary>
public class WebhookSubscription : AuditableEntity, ISoftDeletable
{
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<WebhookSubscriptionEvent> Events { get; set; } = new List<WebhookSubscriptionEvent>();
}

/// <summary>One event type a <see cref="WebhookSubscription"/> is subscribed to (e.g. "order.placed").</summary>
public class WebhookSubscriptionEvent : BaseEntity
{
    public Guid SubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }
    public string EventType { get; set; } = string.Empty;
}

/// <summary>
/// A queued/attempted delivery of one event to one subscription (REQ-PLT-003). Retried with exponential
/// backoff up to a configurable maximum, then marked permanently failed. History is queryable by admins.
/// </summary>
public class WebhookDelivery : AuditableEntity
{
    public Guid SubscriptionId { get; set; }
    public WebhookSubscription? Subscription { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int AttemptCount { get; set; }
    public int? LastResponseStatus { get; set; }
    public string? LastResponseBody { get; set; }
    public DateTime? LastAttemptOnUtc { get; set; }
    public DateTime? NextAttemptOnUtc { get; set; }
}
