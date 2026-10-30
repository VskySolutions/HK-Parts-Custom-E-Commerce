using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Webhooks;

/// <summary>A registered webhook endpoint. <see cref="Secret"/> is returned only when first created.</summary>
public class WebhookSubscriptionDto
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public List<string> EventTypes { get; set; } = new();
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>The signing secret — populated only on creation; never returned by list/get.</summary>
    public string? Secret { get; set; }

    public static WebhookSubscriptionDto From(WebhookSubscription s, bool includeSecret = false) => new()
    {
        Id = s.Id,
        Url = s.Url,
        IsActive = s.IsActive,
        Description = s.Description,
        EventTypes = s.Events.Select(e => e.EventType).OrderBy(e => e).ToList(),
        CreatedOnUtc = s.CreatedOnUtc,
        Secret = includeSecret ? s.Secret : null,
    };
}

/// <summary>A single webhook delivery record with its latest attempt outcome.</summary>
public class WebhookDeliveryDto
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public WebhookDeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public int? LastResponseStatus { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? LastAttemptOnUtc { get; set; }
    public DateTime? NextAttemptOnUtc { get; set; }

    public static WebhookDeliveryDto From(WebhookDelivery d) => new()
    {
        Id = d.Id,
        SubscriptionId = d.SubscriptionId,
        EventType = d.EventType,
        Status = d.Status,
        AttemptCount = d.AttemptCount,
        LastResponseStatus = d.LastResponseStatus,
        OccurredAtUtc = d.OccurredAtUtc,
        LastAttemptOnUtc = d.LastAttemptOnUtc,
        NextAttemptOnUtc = d.NextAttemptOnUtc,
    };
}
