using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A built-in (non-carrier) shipping method: flat rate, weight-based, price-based, or free-shipping
/// threshold (REQ-SHP-003). Weight/price tiers are stored as JSON in <see cref="TiersJson"/>.
/// </summary>
public class ShippingMethod : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public ShippingMethodType MethodType { get; set; }

    /// <summary>
    /// Cost for <see cref="ShippingMethodType.FlatRate"/>. For <see cref="ShippingMethodType.SizeBased"/> it
    /// is the price of an item whose shipping size is blank or not in the tier list (legacy $9.50).
    /// </summary>
    public decimal? FlatRate { get; set; }

    /// <summary>
    /// <see cref="ShippingMethodType.FlatRate"/> only: false charges <see cref="FlatRate"/> once per order,
    /// true charges it per shippable item (gift certificates excluded).
    /// </summary>
    public bool FlatRatePerItem { get; set; }

    /// <summary>
    /// <see cref="ShippingMethodType.SizeBased"/> only: flat charge for the FFL items of a cart (legacy
    /// $40.00), added to the size-tier price of the other items. Null prices FFL items by size like the rest.
    /// </summary>
    public decimal? FflFlatRate { get; set; }

    /// <summary>
    /// <see cref="ShippingMethodType.SizeBased"/> only: price the non-FFL items of a non-US shipment with the
    /// cheapest USPS international service instead of the size tiers. When USPS quotes nothing the method is
    /// withheld rather than offered free.
    /// </summary>
    public bool InternationalViaUsps { get; set; }

    /// <summary>
    /// <see cref="InternationalViaUsps"/> only: price charged when USPS cannot quote (US-23). Null withholds
    /// the method, which blocks checkout with "no delivery options" when nothing else quotes — never $0.
    /// </summary>
    public decimal? InternationalFallbackRate { get; set; }

    /// <summary>Order-subtotal threshold that unlocks free shipping.</summary>
    public decimal? FreeShippingThreshold { get; set; }
    /// <summary>
    /// JSON tier array. Weight/price-based: [{"upTo":5,"rate":9.99}] (the admin UI's shape) or
    /// [{"min":0,"max":5,"rate":9.99}]. Size-based: [{"size":"Small","rate":9.50}], keyed by the product's
    /// "Shipping Package Size" option value.
    /// </summary>
    public string? TiersJson { get; set; }

    /// <summary>
    /// Estimated delivery time in days. Live carriers report this per service; a custom method has no
    /// carrier to ask, so the admin states it here. Null means unknown, which the automatic selector treats
    /// as <see cref="ShippingProviderConfiguration.AssumedTransitDays"/> rather than as instant.
    /// </summary>
    public int? TransitDays { get; set; }

    // Common method settings (AD-04), the same three every carrier method has.
    /// <summary>Added to the method's computed rate (free shipping included). Null = none.</summary>
    public decimal? HandlingFee { get; set; }

    /// <summary>The method is offered only when the order subtotal is at least this. Null = no minimum.</summary>
    public decimal? MinimumOrderAmount { get; set; }

    /// <summary>Comma-separated ISO country codes the method ships to. Null = all countries.</summary>
    public string? ShipToCountries { get; set; }

    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ShippingMethodZoneRate> ZoneRates { get; set; } = new List<ShippingMethodZoneRate>();
}

/// <summary>A geographic shipping zone by country + optional region/postal-code range (AC-SHP-003.5).</summary>
public class ShippingZone : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? Region { get; set; }
    public string? PostalCodeStart { get; set; }
    public string? PostalCodeEnd { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ShippingMethodZoneRate> MethodRates { get; set; } = new List<ShippingMethodZoneRate>();
}

/// <summary>A per-zone rate override for a shipping method (AC-SHP-003.5).</summary>
public class ShippingMethodZoneRate : BaseEntity
{
    public Guid ShippingMethodId { get; set; }
    public ShippingMethod? ShippingMethod { get; set; }
    public Guid ShippingZoneId { get; set; }
    public ShippingZone? ShippingZone { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>
/// Singleton configuration for shipping rate sourcing. Unlike tax — which resolves exactly one active
/// provider — shipping fans out to every enabled source and offers the union at checkout, so the selection
/// is a set rather than a single value (see <see cref="Carriers"/>).
/// </summary>
public class ShippingProviderConfiguration : AuditableEntity
{
    /// <summary>Master switch: when false, no rates are quoted from any source.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether a best-value option is recommended to the customer, or they choose unaided.</summary>
    public ShippingSelectionMode SelectionMode { get; set; } = ShippingSelectionMode.Manual;

    /// <summary>
    /// Balance between cost and speed when scoring options in
    /// <see cref="ShippingSelectionMode.Automatic"/>: 100 = cost is all that matters, 0 = speed is.
    /// </summary>
    public int CostVsSpeedWeight { get; set; } = 50;

    /// <summary>
    /// Delivery estimate assumed for an option whose transit time is unknown. Carriers do not always return
    /// one (and a custom method may have none set), so without this an unknown option would score as though
    /// it were instant and win every time.
    /// </summary>
    public int AssumedTransitDays { get; set; } = 7;

    /// <summary>
    /// Platform switch for collect-in-store. Independent of <see cref="IsEnabled"/> — pickup quotes no
    /// carrier rates, so a store can still offer collection while delivery is switched off entirely. Each
    /// store additionally opts in via <see cref="Store.PickupEnabled"/>; this only decides whether the
    /// choice is offered at all.
    /// </summary>
    public bool PickupEnabled { get; set; } = true;

    public ICollection<ShippingCarrierSetting> Carriers { get; set; } = new List<ShippingCarrierSetting>();
}

/// <summary>
/// Per-carrier enablement for <see cref="ShippingProviderConfiguration"/>. One row per
/// <see cref="ShippingCarrierType"/>; a carrier with no row is treated as disabled.
/// </summary>
public class ShippingCarrierSetting : BaseEntity
{
    public Guid ShippingProviderConfigurationId { get; set; }
    public ShippingProviderConfiguration? Configuration { get; set; }

    public ShippingCarrierType Carrier { get; set; }
    public bool IsEnabled { get; set; }

    /// <summary>Orders the carrier's options within the aggregated quote.</summary>
    public int DisplayOrder { get; set; }
}
