namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Lifecycle of a payment record (REQ-PAY-001/002/003).</summary>
public enum PaymentStatus
{
    Pending = 0,
    Authorized = 1,
    Captured = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5,
    Voided = 6,
    AwaitingPayment = 7,

    /// <summary>No payment is expected — the record is an inquiry (quote request), not a sale.</summary>
    NotRequired = 8,

    /// <summary>
    /// The gateway's fraud filters held the transaction for review (Authorize.Net responseCode 4). Neither
    /// approved nor declined: the merchant decides in the gateway, then the status is synced back (AN-16).
    /// </summary>
    HeldForReview = 9
}

/// <summary>The kind of gateway operation recorded in a <see cref="Entities.PaymentTransaction"/> row.</summary>
public enum PaymentTransactionType
{
    /// <summary>Authorize and capture in one step.</summary>
    Sale = 0,
    /// <summary>Authorization only (funds held).</summary>
    Authorization = 1,
    Capture = 2,
    Void = 3,
    Refund = 4,
    /// <summary>A gateway status lookup that changed the payment (e.g. a held transaction approved).</summary>
    StatusSync = 5
}

/// <summary>Supported payment methods (AC-PAY-001.4/001.5).</summary>
public enum PaymentMethodType
{
    Stripe = 0,
    PayPal = 1,
    Razorpay = 2,
    Square = 3,
    AuthorizeNet = 4,
    CashOnDelivery = 5,
    BankTransfer = 6,
    /// <summary>Paid on eBay (imported eBay order); never charged through a gateway here.</summary>
    Ebay = 7,
    /// <summary>Paid (fully or partly) with a store gift certificate; settled internally, never via a gateway.</summary>
    GiftCertificate = 8,
    /// <summary>The order total is 0.00 (e.g. a 100 % coupon): placed without any payment (PY-40..42).</summary>
    NoPaymentRequired = 9
}

/// <summary>Per-gateway capture mode (REQ-PAY-002).</summary>
public enum CaptureMode
{
    AuthorizeAndCapture = 0,
    AuthorizeOnly = 1
}
