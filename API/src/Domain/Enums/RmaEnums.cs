namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Lifecycle of a return merchandise authorization (REQ-ORD-004).</summary>
public enum RmaStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>How an approved return is resolved (AC-ORD-004.3).</summary>
public enum RmaResolution
{
    None = 0,
    Refund = 1,
    Replacement = 2,
    StoreCredit = 3
}
