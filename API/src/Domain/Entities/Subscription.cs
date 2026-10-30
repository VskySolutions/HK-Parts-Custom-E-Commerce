using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A registered customer's recurring order for a single product/variant (REQ-ORD-005). The background
/// <c>SubscriptionOrderWorker</c> generates a new order at each <see cref="Interval"/> while the
/// subscription is <see cref="SubscriptionStatus.Active"/> and <see cref="NextOrderOnUtc"/> is due.
/// Subscribers can pause, change the interval, or cancel; a recurring-order failure pauses the
/// subscription and notifies the subscriber (AC-ORD-005.4/5).
/// </summary>
public class Subscription : AuditableEntity, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Guid ProductId { get; set; }

    /// <summary>The specific variant subscribed to, for a variant product; null for a simple product.</summary>
    public Guid? ProductVariantId { get; set; }

    public int Quantity { get; set; } = 1;

    public SubscriptionInterval Interval { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

    /// <summary>When the next recurring order is due; advanced by one <see cref="Interval"/> after each generation.</summary>
    public DateTime NextOrderOnUtc { get; set; }

    /// <summary>When the most recent recurring order was generated; null until the first one is produced.</summary>
    public DateTime? LastOrderOnUtc { get; set; }

    /// <summary>The buyer's saved shipping address snapshotted onto each generated order (AC-ORD-005.3).</summary>
    public Guid? ShippingAddressId { get; set; }

    /// <summary>
    /// Opaque reference to the buyer's saved payment method (AC-ORD-005.3). Stored for the future
    /// recurring-capture flow; no live capture is performed today (generated orders are left Pending).
    /// </summary>
    public string? PaymentMethodRef { get; set; }

    /// <summary>Count of consecutive recurring-order failures; incremented when a generation error pauses the sub.</summary>
    public int FailureCount { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public Product? Product { get; set; }
    public Customer? Customer { get; set; }
}
