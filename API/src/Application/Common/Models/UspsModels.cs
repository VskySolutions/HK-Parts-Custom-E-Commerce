namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>
/// USPS REST mail classes the store can offer (docs/usps-shipping-requirements.md US-16, US-24). Web Tools
/// First-Class Package, Parcel Select Ground and Standard Post are all folded into USPS Ground Advantage.
/// </summary>
public static class UspsMailClasses
{
    /// <summary>
    /// Domestic services. A plain code is the mail class at its single-piece rate; "MAILCLASS:XX" is the same
    /// class priced with USPS rate indicator XX (the flat-rate packaging the legacy Web Tools CLASSIDs listed
    /// separately). See <see cref="Parse"/>.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Domestic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["USPS_GROUND_ADVANTAGE"] = "Ground Advantage",
        ["PRIORITY_MAIL"] = "Priority Mail",
        ["PRIORITY_MAIL:FE"] = "Priority Mail Flat Rate Envelope",
        ["PRIORITY_MAIL:FP"] = "Priority Mail Padded Flat Rate Envelope",
        ["PRIORITY_MAIL:FA"] = "Priority Mail Legal Flat Rate Envelope",
        ["PRIORITY_MAIL:FS"] = "Priority Mail Small Flat Rate Box",
        ["PRIORITY_MAIL:FB"] = "Priority Mail Medium Flat Rate Box",
        ["PRIORITY_MAIL:PL"] = "Priority Mail Large Flat Rate Box",
        ["PRIORITY_MAIL_EXPRESS"] = "Priority Mail Express",
        ["PRIORITY_MAIL_EXPRESS:E4"] = "Priority Mail Express Flat Rate Envelope",
        ["PRIORITY_MAIL_EXPRESS:E6"] = "Priority Mail Express Legal Flat Rate Envelope",
        ["PRIORITY_MAIL_EXPRESS:FP"] = "Priority Mail Express Padded Flat Rate Envelope",
        ["PARCEL_SELECT"] = "Parcel Select",
        ["MEDIA_MAIL"] = "Media Mail",
        ["LIBRARY_MAIL"] = "Library Mail",
        ["BOUND_PRINTED_MATTER"] = "Bound Printed Matter",
    };

    /// <summary>
    /// Splits a domestic service code into the mail class and rate indicator sent to USPS. A plain class uses
    /// its single-piece indicator: "PA" for Priority Mail Express, "SP" for everything else.
    /// </summary>
    public static (string MailClass, string RateIndicator) Parse(string code)
    {
        var parts = code.Split(':', 2, StringSplitOptions.TrimEntries);
        var mailClass = parts[0].ToUpperInvariant();
        if (parts.Length == 2 && parts[1].Length > 0)
            return (mailClass, parts[1].ToUpperInvariant());
        return (mailClass, mailClass == "PRIORITY_MAIL_EXPRESS" ? "PA" : "SP");
    }

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
