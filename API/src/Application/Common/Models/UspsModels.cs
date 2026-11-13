namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// USPS REST mail classes the store can offer (docs/usps-shipping-requirements.md US-16, US-24). Web Tools
/// First-Class Package, Parcel Select Ground and Standard Post are all folded into USPS Ground Advantage.
/// </summary>
public static class UspsMailClasses
{
    public static readonly IReadOnlyDictionary<string, string> Domestic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["USPS_GROUND_ADVANTAGE"] = "Ground Advantage",
        ["PRIORITY_MAIL"] = "Priority Mail",
        ["PRIORITY_MAIL_EXPRESS"] = "Priority Mail Express",
        ["PARCEL_SELECT"] = "Parcel Select",
        ["MEDIA_MAIL"] = "Media Mail",
        ["LIBRARY_MAIL"] = "Library Mail",
        ["BOUND_PRINTED_MATTER"] = "Bound Printed Matter",
    };

    public static readonly IReadOnlyDictionary<string, string> International = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["PRIORITY_MAIL_INTERNATIONAL"] = "Priority Mail International",
        ["PRIORITY_MAIL_EXPRESS_INTERNATIONAL"] = "Priority Mail Express International",
        ["FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE"] = "First-Class Package International",
    };

    /// <summary>US-16 default allowed domestic list.</summary>
    public static readonly IReadOnlyList<string> DefaultDomestic =
        new[] { "USPS_GROUND_ADVANTAGE", "PRIORITY_MAIL", "PRIORITY_MAIL_EXPRESS" };

    /// <summary>US-24 default allowed international list.</summary>
    public static readonly IReadOnlyList<string> DefaultInternational =
        new[] { "PRIORITY_MAIL_INTERNATIONAL", "PRIORITY_MAIL_EXPRESS_INTERNATIONAL", "FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE" };

    public static readonly IReadOnlyList<string> PriceTypes = new[] { "RETAIL", "COMMERCIAL" };

    public static string NameOf(string mailClass)
        => Domestic.TryGetValue(mailClass, out var d) ? d
         : International.TryGetValue(mailClass, out var i) ? i
         : mailClass.Replace('_', ' ');

    /// <summary>Splits a stored comma-separated list into trimmed, upper-cased, de-duplicated values.</summary>
    public static List<string> SplitList(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => s.ToUpperInvariant())
                .Distinct()
                .ToList();

    public static string? JoinList(IEnumerable<string>? values)
    {
        var list = values?
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();
        return list is { Count: > 0 } ? string.Join(",", list) : null;
    }
}
