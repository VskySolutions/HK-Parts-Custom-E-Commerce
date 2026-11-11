using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Singleton "Shipping Rules" (AD-20 / AD-21): the checkout add-on fees and the destination restrictions
/// the legacy site hard-coded. Read when a checkout is quoted and again when it is placed. Every save bumps
/// <see cref="Version"/>, and each placed order records the version that priced it (OP-22). When no row
/// exists the defaults below apply; they are the legacy site's values.
/// </summary>
public class ShippingRuleSettings : AuditableEntity
{
    /// <summary>The buyer may ask for a signature on delivery (SR-80).</summary>
    public bool SignatureEnabled { get; set; } = true;
    public decimal SignatureFee { get; set; } = 5.00m;

    /// <summary>The buyer may insure the shipment, priced by declared value (SR-81).</summary>
    public bool InsuranceEnabled { get; set; } = true;

    /// <summary>
    /// Insurance bands as JSON: <c>[{"upTo":50,"fee":2.75}, …]</c>, ascending by <c>upTo</c>. A declared
    /// value above the last band pays the last fee plus <see cref="InsuranceExcessFeePer100"/> for each $100
    /// (or part of $100) above it. Null means the legacy table (DefaultInsuranceBandsJson).
    /// </summary>
    public string? InsuranceBandsJson { get; set; }
    public decimal InsuranceExcessFeePer100 { get; set; } = 1.90m;

    /// <summary>Refuse state-restricted products (spec "Restrict Non High Capacity Mags States" = Yes) shipped to <see cref="RestrictedStates"/> (SR-92).</summary>
    public bool StateRestrictionEnabled { get; set; } = true;

    /// <summary>Comma-separated state / region codes, e.g. "CA,CO,CT".</summary>
    public string RestrictedStates { get; set; } = DefaultRestrictedStates;

    /// <summary>Refuse a non-US address when a cart product has "Allow International Shipping" = No (SR-91).</summary>
    public bool InternationalRestrictionEnabled { get; set; } = true;

    /// <summary>Comma-separated ISO country codes that may be shipped to (SR-90). Null or empty = any country.</summary>
    public string? AllowedCountryCodes { get; set; }

    /// <summary>Refuse P.O. box addresses (SR-94), showing <see cref="PoBoxMessage"/>.</summary>
    public bool RefusePoBoxes { get; set; }
    public string? PoBoxMessage { get; set; }

    /// <summary>Message shown when a destination rule refuses the address. Null = a built-in message per rule.</summary>
    public string? InvalidDestinationMessage { get; set; }

    /// <summary>Incremented on every save; stamped on each order's shipping snapshot.</summary>
    public int Version { get; set; } = 1;

    public const string DefaultRestrictedStates = "CA,CO,CT,IL,DC,HI,MD,MA,NJ,NY,WA";

    /// <summary>The fees the legacy site charges (ShippingFees.php), per open question SR-82.</summary>
    public const string DefaultInsuranceBandsJson =
        "[{\"upTo\":50,\"fee\":2.75},{\"upTo\":100,\"fee\":3.50},{\"upTo\":200,\"fee\":4.60},{\"upTo\":300,\"fee\":6.05}," +
        "{\"upTo\":400,\"fee\":7.60},{\"upTo\":500,\"fee\":9.15},{\"upTo\":600,\"fee\":12.25}]";
}
