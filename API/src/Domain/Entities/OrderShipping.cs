using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// The shipping settings of one shipment, frozen onto its order at placement (shipping order-placement
/// requirements, section 8 — legacy <c>ecomm_order_shipping</c>): the chosen method, its price, the signature
/// and insurance add-ons, and who it ships to. Written once and never re-rated, so a later admin change to a
/// method, fee or rule never alters a placed order (OP-20).
///
/// Today an order has exactly one shipment (<see cref="OrderShipmentType.Combined"/>). A mixed FFL / non-FFL
/// cart will become one order with two rows here (one per shipment), not two orders. That keeps one order
/// number and one payment, and avoids splitting coupons, tax and the charge across orders (OP-42/43).
/// </summary>
public class OrderShipping : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public OrderShipmentType ShipmentType { get; set; } = OrderShipmentType.Combined;
    public ShipToType ShipToType { get; set; } = ShipToType.Customer;

    /// <summary>The offered option's id: a ShippingMethod id, a carrier service code, "pickup" or "no_shipping".</summary>
    public string? MethodId { get; set; }

    /// <summary>The method's internal code (OP-14): the custom method's type (e.g. "SizeBased") or the
    /// carrier's service code, so reports can tell services apart without parsing the display name.</summary>
    public string? MethodName { get; set; }

    /// <summary>The name the buyer saw, e.g. "Standard Shipping".</summary>
    public string? DisplayName { get; set; }

    /// <summary>Rate source: "Custom", a carrier name ("USPS", "UPS", …), "Pickup" or "None".</summary>
    public string? Carrier { get; set; }

    /// <summary>The method's price before handling (section 8: "candidate price before handling").</summary>
    public decimal MethodPrice { get; set; }

    /// <summary>The method's handling fee (AD-04) charged on top of <see cref="MethodPrice"/>.</summary>
    public decimal HandlingFee { get; set; }

    public bool SignatureRequired { get; set; }
    public decimal SignatureFee { get; set; }
    public bool InsuranceSelected { get; set; }
    public decimal InsuranceFee { get; set; }

    /// <summary>Sum of this shipment's line totals: the value insurance was banded on (SR-81).</summary>
    public decimal DeclaredValue { get; set; }

    /// <summary>MethodPrice + HandlingFee + SignatureFee + InsuranceFee. Equals the order's ShippingTotal for a single shipment.</summary>
    public decimal ShippingTotal { get; set; }

    public int? EstimatedDeliveryDays { get; set; }
    public bool WasRecommended { get; set; }

    /// <summary>Units shipped in this shipment (gift certificates excluded) and their total weight in lb when known.</summary>
    public int ItemCount { get; set; }
    public decimal? WeightLb { get; set; }

    /// <summary>True when the shipment holds FFL-required items.</summary>
    public bool ContainsFflItems { get; set; }

    /// <summary>FFL dealer the shipment goes to (<see cref="ShipToType.FflDealer"/> only). Reserved for the FFL dealer step.</summary>
    public string? FflDealerName { get; set; }
    public string? FflNumber { get; set; }

    /// <summary>The <see cref="ShippingRuleSettings.Version"/> that priced the add-ons (OP-22).</summary>
    public int RulesVersion { get; set; }
}
