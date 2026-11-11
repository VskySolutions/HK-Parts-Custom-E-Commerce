using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>One insurance band: declared values up to <see cref="UpTo"/> (inclusive) pay <see cref="Fee"/>.</summary>
public record InsuranceBand(decimal UpTo, decimal Fee);

/// <summary>Admin view of the singleton shipping rules (AD-20 / AD-21).</summary>
public class ShippingRulesDto
{
    public bool SignatureEnabled { get; set; }
    public decimal SignatureFee { get; set; }
    public bool InsuranceEnabled { get; set; }
    public List<InsuranceBand> InsuranceBands { get; set; } = new();
    public decimal InsuranceExcessFeePer100 { get; set; }
    public bool StateRestrictionEnabled { get; set; }
    public List<string> RestrictedStates { get; set; } = new();
    public bool InternationalRestrictionEnabled { get; set; }
    public List<string> AllowedCountryCodes { get; set; } = new();
    public bool RefusePoBoxes { get; set; }
    public string? PoBoxMessage { get; set; }
    public string? InvalidDestinationMessage { get; set; }
    public int Version { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }

    public static ShippingRulesDto From(ShippingRuleSettings s) => new()
    {
        SignatureEnabled = s.SignatureEnabled,
        SignatureFee = s.SignatureFee,
        InsuranceEnabled = s.InsuranceEnabled,
        InsuranceBands = ShippingRulesEngine.ParseBands(s.InsuranceBandsJson).ToList(),
        InsuranceExcessFeePer100 = s.InsuranceExcessFeePer100,
        StateRestrictionEnabled = s.StateRestrictionEnabled,
        RestrictedStates = ShippingRulesEngine.SplitCodes(s.RestrictedStates),
        InternationalRestrictionEnabled = s.InternationalRestrictionEnabled,
        AllowedCountryCodes = ShippingRulesEngine.SplitCodes(s.AllowedCountryCodes),
        RefusePoBoxes = s.RefusePoBoxes,
        PoBoxMessage = s.PoBoxMessage,
        InvalidDestinationMessage = s.InvalidDestinationMessage,
        Version = s.Version,
        UpdatedOnUtc = s.UpdatedOnUtc,
    };
}

/// <summary>The cart facts the destination rules need, beyond the address itself.</summary>
public record DestinationCheckInput(
    string? CountryCode,
    string? Region,
    string? Line1,
    string? Line2,
    bool AnyInternationalBlocked,
    bool AnyStateRestricted,
    bool CustomerExempt);

/// <summary>
/// The shipping rules as pure functions: add-on fees (SR-80/81) and destination restrictions (SR-90..94).
/// Shared by the checkout quote and the placement re-check (OP-54), so the two can never disagree.
/// </summary>
public static class ShippingRulesEngine
{
    /// <summary>Product spec "Allow International Shipping": "No" keeps the cart in the US (SR-91).</summary>
    public const string InternationalAttribute = "Allow International Shipping";

    /// <summary>Product spec "Restrict Non High Capacity Mags States": "Yes" refuses the restricted states (SR-92).</summary>
    public const string StateRestrictedAttribute = "Restrict Non High Capacity Mags States";

    /// <summary>The settings row, or the legacy defaults when the admin has never saved one. Read-only.</summary>
    public static async Task<ShippingRuleSettings> LoadAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.ShippingRuleSettings.AsNoTracking().OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct)
        ?? new ShippingRuleSettings();

    public static IReadOnlyList<InsuranceBand> ParseBands(string? json)
    {
        var source = string.IsNullOrWhiteSpace(json) ? ShippingRuleSettings.DefaultInsuranceBandsJson : json;
        try
        {
            var bands = JsonSerializer.Deserialize<List<InsuranceBand>>(source, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (bands is { Count: > 0 })
                return bands.OrderBy(b => b.UpTo).ToList();
        }
        catch (JsonException)
        {
            // A hand-edited, unreadable row falls back to the legacy table rather than pricing insurance at 0.
        }

        return JsonSerializer.Deserialize<List<InsuranceBand>>(ShippingRuleSettings.DefaultInsuranceBandsJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public static string SerializeBands(IEnumerable<InsuranceBand> bands) =>
        JsonSerializer.Serialize(
            bands.OrderBy(b => b.UpTo).Select(b => new { upTo = b.UpTo, fee = b.Fee }));

    /// <summary>
    /// Insurance fee for a declared value (SR-81): the first band whose limit covers it; above the last band,
    /// the last fee plus <paramref name="excessPer100"/> for each $100 or part of $100 over that limit.
    /// </summary>
    public static decimal InsuranceFee(decimal declaredValue, IReadOnlyList<InsuranceBand> bands, decimal excessPer100)
    {
        if (bands.Count == 0) return 0m;
        var value = Math.Max(0m, declaredValue);
        foreach (var band in bands)
            if (value <= band.UpTo)
                return band.Fee;

        var last = bands[^1];
        var steps = Math.Ceiling((value - last.UpTo) / 100m);
        return decimal.Round(last.Fee + steps * excessPer100, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The first destination rule the address breaks, as a buyer-facing message; null when every rule passes.
    /// All rules are checked independently, so an allowed state never excuses a disallowed country (SR-93).
    /// </summary>
    public static string? CheckDestination(ShippingRuleSettings rules, DestinationCheckInput input)
    {
        var country = (input.CountryCode ?? string.Empty).Trim().ToUpperInvariant();

        var allowed = SplitCodes(rules.AllowedCountryCodes);
        if (allowed.Count > 0 && !allowed.Contains(country))
            return rules.InvalidDestinationMessage ?? "Sorry, we do not ship to this country.";

        if (rules.InternationalRestrictionEnabled && input.AnyInternationalBlocked && country is not ("" or "US"))
            return rules.InvalidDestinationMessage
                   ?? "One or more items in your cart can only be shipped within the United States.";

        if (rules.StateRestrictionEnabled && input.AnyStateRestricted && !input.CustomerExempt && country is ("" or "US"))
        {
            var state = NormalizeUsState(input.Region);
            if (state is not null && SplitCodes(rules.RestrictedStates).Contains(state))
                return rules.InvalidDestinationMessage
                       ?? $"One or more items in your cart cannot be shipped to {state}.";
        }

        if (rules.RefusePoBoxes && (IsPoBox(input.Line1) || IsPoBox(input.Line2)))
            return string.IsNullOrWhiteSpace(rules.PoBoxMessage)
                ? "We cannot ship to a P.O. box. Please enter a street address."
                : rules.PoBoxMessage;

        return null;
    }

    private static readonly Regex PoBoxPattern = new(
        @"\b(p\.?\s*o\.?\s*box|post\s+office\s+box|p\.?\s*o\.?\s*b\.?\b|pobox)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsPoBox(string? line) => !string.IsNullOrWhiteSpace(line) && PoBoxPattern.IsMatch(line);

    public static List<string> SplitCodes(string? csv) =>
        (csv ?? string.Empty)
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(c => c.Trim().ToUpperInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();

    /// <summary>A US state as its two-letter code. The storefront's state field is free text, so full names are mapped too.</summary>
    public static string? NormalizeUsState(string? region)
    {
        if (string.IsNullOrWhiteSpace(region)) return null;
        var r = region.Trim();
        if (r.Length == 2) return r.ToUpperInvariant();
        return UsStates.TryGetValue(r, out var code) ? code : r.ToUpperInvariant();
    }

    private static readonly Dictionary<string, string> UsStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alabama"] = "AL", ["Alaska"] = "AK", ["Arizona"] = "AZ", ["Arkansas"] = "AR", ["California"] = "CA",
        ["Colorado"] = "CO", ["Connecticut"] = "CT", ["Delaware"] = "DE", ["District of Columbia"] = "DC",
        ["Washington DC"] = "DC", ["Washington D.C."] = "DC", ["Florida"] = "FL", ["Georgia"] = "GA",
        ["Hawaii"] = "HI", ["Idaho"] = "ID", ["Illinois"] = "IL", ["Indiana"] = "IN", ["Iowa"] = "IA",
        ["Kansas"] = "KS", ["Kentucky"] = "KY", ["Louisiana"] = "LA", ["Maine"] = "ME", ["Maryland"] = "MD",
        ["Massachusetts"] = "MA", ["Michigan"] = "MI", ["Minnesota"] = "MN", ["Mississippi"] = "MS",
        ["Missouri"] = "MO", ["Montana"] = "MT", ["Nebraska"] = "NE", ["Nevada"] = "NV", ["New Hampshire"] = "NH",
        ["New Jersey"] = "NJ", ["New Mexico"] = "NM", ["New York"] = "NY", ["North Carolina"] = "NC",
        ["North Dakota"] = "ND", ["Ohio"] = "OH", ["Oklahoma"] = "OK", ["Oregon"] = "OR", ["Pennsylvania"] = "PA",
        ["Rhode Island"] = "RI", ["South Carolina"] = "SC", ["South Dakota"] = "SD", ["Tennessee"] = "TN",
        ["Texas"] = "TX", ["Utah"] = "UT", ["Vermont"] = "VT", ["Virginia"] = "VA", ["Washington"] = "WA",
        ["West Virginia"] = "WV", ["Wisconsin"] = "WI", ["Wyoming"] = "WY", ["Puerto Rico"] = "PR",
    };

    /// <summary>Formats an amount for a buyer-facing message ("16.50").</summary>
    public static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
}
