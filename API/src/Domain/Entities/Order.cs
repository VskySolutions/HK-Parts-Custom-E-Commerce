using System.ComponentModel.DataAnnotations.Schema;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A minimal customer order used by the routing engine (WO-51) and store order queue (WO-52). Carries
/// the delivery address (for routing), the assigned store, line items, and a lifecycle
/// <see cref="OrderStatus"/>. Full checkout/payment/pricing is a separate future feature — this model
/// is intentionally lean and additive.
/// </summary>
public class Order : AuditableEntity, ISoftDeletable
{
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Null for a guest order.</summary>
    public Guid? CustomerId { get; set; }

    /// <summary>The cart this order was placed from — consumed (checked out) only once payment succeeds
    /// (redirect gateways complete after the order is created), so a cancelled payment keeps the cart.</summary>
    public Guid? SourceCartId { get; set; }
    public Customer? Customer { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // Delivery target (drives routing) — the postal address is a shared Address row (WO: address centralization).
    public Guid? ShippingAddressId { get; set; }
    public Address? ShippingAddress { get; set; }

    // Read-through helpers over the linked address (require ShippingAddress to be Include()d; never mapped
    // to columns). Keep the rest of the order lifecycle (notifications, DTOs, routing) unchanged.
    [NotMapped] public string? ContactName => ShippingAddress is null ? null : $"{ShippingAddress.FirstName} {ShippingAddress.LastName}".Trim();
    [NotMapped] public string? ContactEmail => ShippingAddress?.Email;
    [NotMapped] public string? ContactPhone => ShippingAddress?.PhoneNumber;
    [NotMapped] public double? Latitude => ShippingAddress?.Latitude;
    [NotMapped] public double? Longitude => ShippingAddress?.Longitude;
    [NotMapped] public string? CountryCode => ShippingAddress?.CountryCode;
    [NotMapped] public string? Region => ShippingAddress?.StateProvince;
    [NotMapped] public string? PostalCode => ShippingAddress?.PostalCode;
    [NotMapped] public string? AddressLine1 => ShippingAddress?.AddressLine1;
    [NotMapped] public string? AddressLine2 => ShippingAddress?.AddressLine2;
    [NotMapped] public string? Landmark => ShippingAddress?.Landmark;
    [NotMapped] public string? City => ShippingAddress?.City;
    [NotMapped] public string? StateProvince => ShippingAddress?.StateProvince;

    // Routing outcome.
    public Guid? AssignedStoreId { get; set; }

    /// <summary>True when the order is fulfilled by pickup-in-store rather than carrier delivery (REQ-SHP-004).</summary>
    public bool IsPickup { get; set; }
    public Store? AssignedStore { get; set; }
    public DateTime PlacedOnUtc { get; set; }
    public DateTime? RoutedOnUtc { get; set; }

    /// <summary>JSON array of store ids already attempted, so a rejection re-routes to the next store (AC-STR-003.4).</summary>
    public string? ExcludedStoreIdsJson { get; set; }

    // Financial breakdown, captured at placement (WO-30/45). TotalAmount is the grand total.
    public string CurrencyCode { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }

    /// <summary>The payment-gateway transaction fee added to this order as an additional charge: the
    /// percentage applied (from the gateway's integration config) and the resulting amount. Both 0 when the
    /// chosen method has no fee. Included in <see cref="TotalAmount"/>.</summary>
    public decimal PaymentFeePercent { get; set; }
    public decimal PaymentFeeTotal { get; set; }

    public decimal TotalAmount { get; set; }

    /// <summary>Immutable jurisdiction-level tax breakdown JSON, stored at placement time and never recalculated (Order Management ADR).</summary>
    public string? TaxBreakdownJson { get; set; }
    /// <summary>Set when the flat-rate tax fallback was applied and the order needs tax review (AC-TAX-001.4).</summary>
    public bool TaxFlaggedForReview { get; set; }
    /// <summary>Provider calculation reference captured at placement, used for transaction reporting (e.g. Stripe Tax calculation id; WO-37).</summary>
    public string? TaxProviderCalculationRef { get; set; }

    public string? AppliedCouponCode { get; set; }

    /// <summary>Loyalty points redeemed against this order at placement, and the discount value they bought
    /// (folded into <see cref="DiscountTotal"/>) (WO-27). The balance is debited once the order is placed + paid.</summary>
    public int PointsRedeemed { get; set; }
    public decimal PointsDiscountAmount { get; set; }

    // Shipping selection + fulfilment tracking (WO-45).
    /// <summary>
    /// The offered option's MethodId that priced this order — a ShippingMethod id for a custom
    /// method, a carrier service code, or "pickup". Not a foreign key:
    /// most values do not refer to a row we own. Stored so an order can be traced back to what priced it,
    /// which name + carrier alone cannot do once a method is renamed.
    /// </summary>
    public string? ShippingMethodId { get; set; }
    public string? ShippingMethodName { get; set; }
    public string? ShippingCarrier { get; set; }
    /// <summary>True when the shipping option was the automatic recommendation rather than a buyer's pick.</summary>
    public bool ShippingWasRecommended { get; set; }
    public string? TrackingNumber { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    // ---- Inquiry (quote request) ------------------------------------------------------------

    /// <summary>
    /// True when this row is an inquiry rather than a sale: no payment was taken, no stock was
    /// committed, no coupon redeemed. Inquiries carry <see cref="OrderStatus.Inquiry"/> and are kept
    /// out of every order/revenue listing via <c>ExcludeInquiries()</c>.
    /// </summary>
    public bool IsInquiry { get; set; }

    /// <summary>The inquiry sales pipeline; null for a normal order.</summary>
    public InquiryStatus? InquiryStatus { get; set; }

    /// <summary>The buyer's free-text requirements/message from the inquiry form.</summary>
    public string? CustomerNote { get; set; }

    /// <summary>Company the buyer is inquiring on behalf of (B2B lead detail).</summary>
    public string? CompanyName { get; set; }

    /// <summary>How the buyer would rather be contacted about the inquiry.</summary>
    public ContactPreference? PreferredContact { get; set; }

    /// <summary>Optional date the buyer needs the goods by.</summary>
    public DateTime? RequiredByUtc { get; set; }

    /// <summary>Internal, admin-only notes on the inquiry (never shown to the buyer).</summary>
    public string? InternalNotes { get; set; }

    /// <summary>When the inquiry was answered with a quote (stamped by the send-quote action).</summary>
    public DateTime? QuotedOnUtc { get; set; }

    public DateTime? ShippedOnUtc { get; set; }
    public DateTime? DeliveredOnUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<OrderLineItem> Lines { get; set; } = new List<OrderLineItem>();
    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<PaymentRecord> Payments { get; set; } = new List<PaymentRecord>();
}
