namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Delivery state of a single webhook dispatch attempt chain (REQ-PLT-003).</summary>
public enum WebhookDeliveryStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    PermanentlyFailed = 3
}
