namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// Recurrence cadence of a product <see cref="Entities.Subscription"/> (REQ-ORD-005). A product's
/// admin-configured allowed intervals (Product.SubscriptionIntervals CSV) are drawn from this set.
/// </summary>
public enum SubscriptionInterval
{
    /// <summary>Every 7 days.</summary>
    Weekly = 0,
    /// <summary>Every 14 days.</summary>
    BiWeekly = 1,
    /// <summary>Every calendar month.</summary>
    Monthly = 2,
    /// <summary>Every 3 calendar months.</summary>
    Quarterly = 3
}

/// <summary>Lifecycle state of a subscription (REQ-ORD-005). Cancelled is terminal.</summary>
public enum SubscriptionStatus
{
    /// <summary>Generating recurring orders on schedule.</summary>
    Active = 0,
    /// <summary>Paused by the subscriber/admin or after a failed recurring order; generates nothing until resumed.</summary>
    Paused = 1,
    /// <summary>Cancelled by the subscriber/admin; terminal, never generates again.</summary>
    Cancelled = 2
}
