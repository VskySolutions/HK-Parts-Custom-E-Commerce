namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// Lifecycle of an order through routing and store fulfilment (Store Management REQ-STR-003/004).
/// This is the minimal status set needed by the routing engine and store order queue; full Order
/// Management (cart, checkout, payment, returns) is a separate future feature.
/// </summary>
public enum OrderStatus
{
    /// <summary>Created but not yet routed to a store.</summary>
    PendingRouting = 0,
    /// <summary>Assigned to a store and sitting in that store's queue awaiting accept/reject.</summary>
    Routed = 1,
    /// <summary>No eligible store could fulfil the order (AC-STR-003.5).</summary>
    Unrouted = 2,
    /// <summary>The assigned store accepted the order.</summary>
    Accepted = 3,
    /// <summary>The assigned store rejected the order (transient — triggers routing fallback).</summary>
    Rejected = 4,
    /// <summary>Fulfilment in progress at the assigned store.</summary>
    Preparing = 5,
    Shipped = 6,
    Delivered = 7,
    Cancelled = 8,
    /// <summary>Placed and confirmed (post-checkout initial lifecycle state; WO-45 AC-ORD-001.1).</summary>
    Pending = 9,

    /// <summary>A pickup-in-store order that is prepared and awaiting collection (REQ-SHP-004).</summary>
    ReadyForPickup = 11,

    /// <summary>Being prepared for fulfilment (WO-45 lifecycle; synonym of Preparing).</summary>
    Processing = 10,

    /// <summary>
    /// A submitted inquiry (quote request), not yet a real order — no payment was taken and nothing is
    /// reserved. Kept as a dedicated status so every existing query that filters on a fulfilment status
    /// naturally excludes inquiries; the sales pipeline itself lives in <see cref="InquiryStatus"/>.
    /// </summary>
    Inquiry = 12
}

/// <summary>
/// The sales pipeline of an inquiry (an order with <c>IsInquiry</c> set), independent of the fulfilment
/// <see cref="OrderStatus"/>. Only a Standard-mode inquiry reaches <see cref="Converted"/> — an
/// inquiry-only tenant closes the request instead, since there is no online order to convert into.
/// </summary>
public enum InquiryStatus
{
    New = 0,
    InReview = 1,
    Quoted = 2,
    Accepted = 3,
    Declined = 4,
    Converted = 5,
    Closed = 6
}

/// <summary>How the buyer would prefer to be contacted about their inquiry.</summary>
public enum ContactPreference
{
    Email = 0,
    Phone = 1,
    WhatsApp = 2
}
