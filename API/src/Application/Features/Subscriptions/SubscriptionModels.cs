using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Subscriptions;

/// <summary>
/// View of a product subscription (REQ-ORD-005). Customer and product names are only populated when the
/// <see cref="Subscription.Customer"/> / <see cref="Subscription.Product"/> navigations are Include()d.
/// </summary>
public class SubscriptionDto
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public Guid ProductId { get; set; }
    public string? ProductName { get; set; }
    public Guid? ProductVariantId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Recurrence interval name (Weekly/BiWeekly/Monthly/Quarterly).</summary>
    public string Interval { get; set; } = string.Empty;

    /// <summary>Lifecycle status name (Active/Paused/Cancelled).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime NextOrderOnUtc { get; set; }
    public DateTime? LastOrderOnUtc { get; set; }

    public Guid? ShippingAddressId { get; set; }
    public int FailureCount { get; set; }

    public DateTime CreatedOnUtc { get; set; }

    public static SubscriptionDto From(Subscription s) => new()
    {
        Id = s.Id,
        CustomerId = s.CustomerId,
        CustomerName = s.Customer is null ? null : $"{s.Customer.FirstName} {s.Customer.LastName}".Trim(),
        ProductId = s.ProductId,
        ProductName = s.Product?.Name,
        ProductVariantId = s.ProductVariantId,
        Quantity = s.Quantity,
        Interval = s.Interval.ToString(),
        Status = s.Status.ToString(),
        NextOrderOnUtc = s.NextOrderOnUtc,
        LastOrderOnUtc = s.LastOrderOnUtc,
        ShippingAddressId = s.ShippingAddressId,
        FailureCount = s.FailureCount,
        CreatedOnUtc = s.CreatedOnUtc,
    };
}
