namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// Lifecycle of a return merchandise authorization (order-return-requirements RT-01). The numeric values of
/// the original five states are kept so existing rows keep their meaning (the old "Completed" = 3 is now
/// <see cref="Closed"/>). Allowed transitions live in <c>RmaWorkflow</c>.
/// </summary>
public enum RmaStatus
{
    Requested = 0,
    Approved = 1,
    Rejected = 2,
    Closed = 3,
    Cancelled = 4,
    InTransit = 5,
    Received = 6,
    Inspected = 7,
    Refunded = 8,
    Exchanged = 9,
    Expired = 10
}

/// <summary>How a return is resolved: what the customer asked for and, once settled, what was done.</summary>
public enum RmaResolution
{
    None = 0,
    /// <summary>Refund to the original payment method(s).</summary>
    Refund = 1,
    /// <summary>Exchange: a no-charge replacement order for the returned items.</summary>
    Replacement = 2,
    /// <summary>A new gift certificate for the refund total (RT-69).</summary>
    StoreCredit = 3
}

/// <summary>How the goods come back (request form, section 6.2).</summary>
public enum RmaReturnMethod
{
    /// <summary>The store supplies a prepaid return label.</summary>
    PrepaidLabel = 0,
    /// <summary>The customer ships it themselves and may enter the tracking number (RT-45).</summary>
    SelfShip = 1,
    /// <summary>Brought to a store counter.</summary>
    DropOff = 2
}

/// <summary>Where the RMA was created.</summary>
public enum RmaSource
{
    Customer = 0,
    Guest = 1,
    Admin = 2,
    Api = 3
}

/// <summary>The condition the customer says the item is in.</summary>
public enum RmaItemCondition
{
    Unopened = 0,
    OpenedUnused = 1,
    InstalledUsed = 2
}

/// <summary>The condition the warehouse found on receipt (RT-50).</summary>
public enum RmaReceivedCondition
{
    Resellable = 0,
    OpenBox = 1,
    Damaged = 2,
    Defective = 3,
    WrongItem = 4,
    NotReceived = 5
}

/// <summary>What happens to a received unit (RT-51). Only <see cref="Restock"/> changes inventory.</summary>
public enum RmaDisposition
{
    Restock = 0,
    RestockOpenBox = 1,
    ReturnToVendor = 2,
    Scrap = 3
}

/// <summary>How the money went back to the customer.</summary>
public enum RmaRefundMethod
{
    None = 0,
    /// <summary>Through the gateway(s) of the original payment(s).</summary>
    OriginalPayment = 1,
    /// <summary>Issued as a gift certificate.</summary>
    StoreCredit = 2,
    /// <summary>Refunded outside the system; staff recorded a reference (RT-61).</summary>
    Manual = 3
}

/// <summary>Who wrote a history row or message on an RMA.</summary>
public enum RmaActorType
{
    System = 0,
    Customer = 1,
    Admin = 2
}

/// <summary>Return-label policy (RT-40).</summary>
public enum ReturnLabelMode
{
    Disabled = 0,
    CustomerShips = 1,
    /// <summary>The store sends a prepaid label and pays for it.</summary>
    PrepaidMerchant = 2,
    /// <summary>The store sends a prepaid label and deducts its cost from the refund (waived for merchant-fault reasons).</summary>
    PrepaidDeducted = 3
}
