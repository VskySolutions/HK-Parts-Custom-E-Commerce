namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Built-in custom shipping method calculation type (REQ-SHP-003).</summary>
public enum ShippingMethodType
{
    FlatRate = 0,
    WeightBased = 1,
    PriceBased = 2,
    FreeShipping = 3,

    /// <summary>
    /// The legacy site's "Custom" method: priced by the highest "Shipping Package Size" tier among the
    /// cart's non-FFL items, plus a flat rate when the cart holds FFL items, and — for a non-US destination —
    /// the cheapest USPS international service in place of the size tiers.
    /// </summary>
    SizeBased = 4
}

/// <summary>
/// A source of shipping rate options. <see cref="Manual"/> covers the built-in custom
/// <see cref="Entities.ShippingMethod"/> rows; the rest are live-carrier integrations. Every value except
/// Manual maps to one ICarrierClient implementation; Manual is evaluated in-process.
/// </summary>
public enum ShippingCarrierType
{
    Manual = 0,
    FedEx = 1,
    DHLExpress = 2,
    USPS = 3,
    UPS = 4,
    /// <summary>LTL freight quotes from Freightview (one option per returned carrier/service).</summary>
    Freightview = 5
}

/// <summary>How the shipping option a customer checks out with is chosen (REQ-SHP-006).</summary>
public enum ShippingSelectionMode
{
    /// <summary>The customer picks from the full list of offered options; nothing is recommended.</summary>
    Manual = 0,

    /// <summary>
    /// The best-value option is scored server-side and offered as the recommended default. The customer can
    /// still override it — automatic drives the default, it does not remove the choice.
    /// </summary>
    Automatic = 1
}

/// <summary>Lifecycle of a physical shipment / carrier package (REQ-ORD-002, REQ-SHP-002).</summary>
public enum ShipmentStatus
{
    Created = 0,
    LabelGenerated = 1,
    Shipped = 2,
    InTransit = 3,
    OutForDelivery = 4,
    Delivered = 5,
    Exception = 6,
    Cancelled = 7
}

/// <summary>Which shipment of an order an <see cref="Entities.OrderShipping"/> row describes.</summary>
public enum OrderShipmentType
{
    /// <summary>The order's only shipment, holding every shippable line (today's checkout).</summary>
    Combined = 0,
    /// <summary>The non-FFL lines of a mixed cart, shipped to the customer.</summary>
    NonFfl = 1,
    /// <summary>The FFL lines of a mixed cart, shipped to the FFL dealer.</summary>
    Ffl = 2
}

/// <summary>Who a shipment is delivered to.</summary>
public enum ShipToType
{
    Customer = 0,
    FflDealer = 1,
    /// <summary>Collected in store; nothing is shipped.</summary>
    StorePickup = 2
}
