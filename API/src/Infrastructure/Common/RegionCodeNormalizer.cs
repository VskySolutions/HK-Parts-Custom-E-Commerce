namespace HKPartsEcommerce.Infrastructure.Common;

/// <summary>
/// Normalizes a US/Canada state or province to its 2-letter ISO 3166-2 code (e.g. "Florida" → "FL").
/// <para>
/// The app's one standard address form (<c>AppAddressForm</c>) stores the state/province as its display
/// <b>name</b> — for every address, store origins and customer destinations alike — whereas the external
/// integrations require the 2-letter code and reject a full name: tax providers with an explicit 400
/// (TaxJar: "from_zip … is not used within from_state FLORIDA"), carriers with an equally unhelpful one.
/// This adapter bridges the app's storage convention to what they expect. Only US/Canada use this code
/// convention; other countries and any unrecognized value are returned trimmed and unchanged so the
/// integration can decide.
/// </para>
/// <para>
/// It is also what makes zone matching survive the same mismatch: an admin who types "CA" into a shipping
/// zone and a buyer whose address says "California" describe the same place, so
/// <see cref="Shipping.ShippingRateService"/> normalizes both sides before comparing rather than trusting
/// either to be stored in a particular form.
/// </para>
/// </summary>
internal static class RegionCodeNormalizer
{
    // State/province name → 2-letter code (US states + DC + common territories, and Canadian provinces).
    // Case-insensitive so "Florida", "FLORIDA" and "florida" all match.
    private static readonly Dictionary<string, string> NameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        // United States
        ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["Arkansas"] = "AR",
        ["California"] = "CA", ["Colorado"] = "CO", ["Connecticut"] = "CT", ["Delaware"] = "DE",
        ["Florida"] = "FL", ["Georgia"] = "GA", ["Hawaii"] = "HI", ["Idaho"] = "ID",
        ["Illinois"] = "IL", ["Indiana"] = "IN", ["Iowa"] = "IA", ["Kansas"] = "KS",
        ["Kentucky"] = "KY", ["Louisiana"] = "LA", ["Maine"] = "ME", ["Maryland"] = "MD",
        ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN", ["Mississippi"] = "MS",
        ["Missouri"] = "MO", ["Montana"] = "MT", ["Nebraska"] = "NE", ["Nevada"] = "NV",
        ["New Hampshire"] = "NH", ["New Jersey"] = "NJ", ["New Mexico"] = "NM", ["New York"] = "NY",
        ["North Carolina"] = "NC", ["North Dakota"] = "ND", ["Ohio"] = "OH", ["Oklahoma"] = "OK",
        ["Oregon"] = "OR", ["Pennsylvania"] = "PA", ["Rhode Island"] = "RI", ["South Carolina"] = "SC",
        ["South Dakota"] = "SD", ["Tennessee"] = "TN", ["Texas"] = "TX", ["Utah"] = "UT",
        ["Vermont"] = "VT", ["Virginia"] = "VA", ["Washington"] = "WA", ["West Virginia"] = "WV",
        ["Wisconsin"] = "WI", ["Wyoming"] = "WY", ["District of Columbia"] = "DC",
        ["Puerto Rico"] = "PR", ["Guam"] = "GU", ["American Samoa"] = "AS",
        ["U.S. Virgin Islands"] = "VI", ["United States Virgin Islands"] = "VI",
        ["Northern Mariana Islands"] = "MP",

        // Canada
        ["Alberta"] = "AB", ["British Columbia"] = "BC", ["Manitoba"] = "MB", ["New Brunswick"] = "NB",
        ["Newfoundland and Labrador"] = "NL", ["Northwest Territories"] = "NT", ["Nova Scotia"] = "NS",
        ["Nunavut"] = "NU", ["Ontario"] = "ON", ["Prince Edward Island"] = "PE",
        ["Quebec"] = "QC", ["Québec"] = "QC", ["Saskatchewan"] = "SK", ["Yukon"] = "YT",
    };

    // Every recognized 2-letter code, so an already-correct value passes straight through.
    private static readonly HashSet<string> KnownCodes =
        new(NameToCode.Values, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the 2-letter state/province code for a US/Canada <paramref name="region"/> supplied as a
    /// name (e.g. "Florida") or already a code (e.g. "FL"). Values for other countries, blanks, and any
    /// unrecognized region are returned trimmed and unchanged.
    /// </summary>
    public static string? ToStateCode(string? country, string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return region;

        var trimmed = region.Trim();

        // The 2-letter state/province convention only applies to the US and Canada.
        if (!IsUsOrCanada(country))
            return trimmed;

        // Already a code we recognize.
        if (trimmed.Length == 2 && KnownCodes.Contains(trimmed))
            return trimmed.ToUpperInvariant();

        // A full name we can map.
        if (NameToCode.TryGetValue(trimmed, out var code))
            return code;

        // Unknown — leave it for the integration to accept or reject (its error will name the field).
        return trimmed;
    }

    private static bool IsUsOrCanada(string? country)
    {
        if (string.IsNullOrWhiteSpace(country))
            return false;

        var c = country.Trim();
        return c.Equals("US", StringComparison.OrdinalIgnoreCase)
            || c.Equals("USA", StringComparison.OrdinalIgnoreCase)
            || c.Equals("United States", StringComparison.OrdinalIgnoreCase)
            || c.Equals("CA", StringComparison.OrdinalIgnoreCase)
            || c.Equals("CAN", StringComparison.OrdinalIgnoreCase)
            || c.Equals("Canada", StringComparison.OrdinalIgnoreCase);
    }
}
