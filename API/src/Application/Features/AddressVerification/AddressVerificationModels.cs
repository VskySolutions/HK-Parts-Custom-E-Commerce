using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.AddressVerification;

/// <summary>Admin view of the Address Verification screen (§5.2), including the P.O. box rule kept on the shipping rules.</summary>
public class AddressVerificationSettingsDto
{
    public AddressVerificationProvider Provider { get; set; }
    public string? FailedMessage { get; set; }
    public List<string> Countries { get; set; } = new();
    public bool OfferSuggestion { get; set; }
    public AddressUnavailablePolicy OnUnavailable { get; set; }
    public bool LoggingEnabled { get; set; }
    public bool BlockPoBox { get; set; }
    public string? PoBoxMessage { get; set; }

    /// <summary>Whether each provider has an active credential (AD-62), keyed "Avalara" / "Usps".</summary>
    public Dictionary<string, bool> ProviderConfigured { get; set; } = new();

    public DateTime? UpdatedOnUtc { get; set; }

    public static AddressVerificationSettingsDto From(AddressVerificationSettings s, ShippingRuleSettings rules) => new()
    {
        Provider = s.Provider,
        FailedMessage = s.FailedMessage,
        Countries = ShippingRulesEngine.SplitCodes(s.Countries),
        OfferSuggestion = s.OfferSuggestion,
        OnUnavailable = s.OnUnavailable,
        LoggingEnabled = s.LoggingEnabled,
        BlockPoBox = rules.RefusePoBoxes,
        PoBoxMessage = rules.PoBoxMessage,
        UpdatedOnUtc = s.UpdatedOnUtc == default ? null : s.UpdatedOnUtc,
    };
}

/// <summary>An address as the storefront and admin send and receive it.</summary>
public class VerificationAddressDto
{
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }

    public static VerificationAddressDto? From(VerifiableAddress? a) => a is null ? null : new()
    {
        Line1 = a.Line1, Line2 = a.Line2, City = a.City, Region = a.Region, PostalCode = a.PostalCode, CountryCode = a.CountryCode,
    };
}

/// <summary>
/// The answer to <c>POST /api/checkout/address/verify</c> (§6.1): <see cref="Status"/> is one of
/// <c>valid</c>, <c>suggested</c>, <c>invalid</c>, <c>unavailable</c>, <c>poBox</c> or <c>notChecked</c>.
/// </summary>
public class CheckoutAddressVerificationDto
{
    public string Status { get; set; } = "notChecked";

    /// <summary>False when the buyer must change the address before continuing.</summary>
    public bool CanContinue { get; set; }

    /// <summary>The buyer-facing headline (the admin's failed / P.O. box message).</summary>
    public string? Message { get; set; }

    /// <summary>The provider's reasons, shown under the headline (AV-11).</summary>
    public List<string> Messages { get; set; } = new();

    /// <summary>"We suggest: …" (AV-13), only when the status is <c>suggested</c>.</summary>
    public VerificationAddressDto? Suggestion { get; set; }

    /// <summary>ProblemDetails code of a refusal: ADDRESS_INVALID, ADDRESS_PO_BOX or ADDRESS_VERIFY_UNAVAILABLE.</summary>
    public string? Code { get; set; }
}

/// <summary>Admin "Test address" result (AD-61).</summary>
public class AddressVerificationTestDto
{
    public AddressVerificationProvider Provider { get; set; }
    public AddressVerificationStatus Status { get; set; }
    public bool IsPoBox { get; set; }
    public bool? IsResidential { get; set; }
    public VerificationAddressDto? Suggestion { get; set; }
    public List<string> Messages { get; set; } = new();
    public string? RawResponse { get; set; }
    public long DurationMs { get; set; }
}

public static class AddressVerificationCodes
{
    public const string Invalid = "ADDRESS_INVALID";
    public const string PoBox = "ADDRESS_PO_BOX";
    public const string Unavailable = "ADDRESS_VERIFY_UNAVAILABLE";

    public const string DefaultFailedMessage =
        "We could not verify your shipping address. Please check it and try again.";
    public const string DefaultUnavailableMessage =
        "We could not verify your shipping address right now. Please try again in a few minutes.";
}

/// <summary>Helpers shared by the settings, the service and checkout.</summary>
public static class AddressVerificationSupport
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromDays(30);

    /// <summary>The settings row, or the defaults (verification off) when never saved. Read-only.</summary>
    public static async Task<AddressVerificationSettings> LoadAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.AddressVerificationSettings.AsNoTracking().OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct)
        ?? new AddressVerificationSettings();

    public static VerifiableAddress ToVerifiable(CheckoutAddress a) =>
        new(a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.CountryCode);

    public static VerifiableAddress ToVerifiable(VerificationAddressDto a) =>
        new(a.Line1, a.Line2, a.City, a.Region, a.PostalCode, a.CountryCode);

    /// <summary>
    /// SHA-256 of the normalised address (AV-06): upper-cased, punctuation dropped, whitespace collapsed, the
    /// state as its two-letter code. "3334 Gray Moss Rd." and "3334  GRAY MOSS RD" hash the same.
    /// </summary>
    public static string Hash(VerifiableAddress a)
    {
        var canonical = string.Join("|",
            Normalize(a.Line1), Normalize(a.Line2), Normalize(a.City),
            Normalize(ShippingRulesEngine.NormalizeUsState(a.Region)),
            Normalize(a.PostalCode), Normalize(a.CountryCode));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static readonly Regex NonWord = new(@"[^\p{L}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string Normalize(string? value) =>
        NonWord.Replace((value ?? string.Empty).ToUpperInvariant(), " ").Trim();

    public static string? SerializeAddress(VerifiableAddress? a) => a is null ? null : JsonSerializer.Serialize(a);

    public static VerifiableAddress? DeserializeAddress(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<VerifiableAddress>(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>The <see cref="CheckoutAddressVerificationDto.Status"/> wire name.</summary>
    public static string WireStatus(AddressVerificationStatus status) => status switch
    {
        AddressVerificationStatus.Valid => "valid",
        AddressVerificationStatus.Suggested => "suggested",
        AddressVerificationStatus.Invalid => "invalid",
        AddressVerificationStatus.Unavailable => "unavailable",
        AddressVerificationStatus.PoBox => "poBox",
        _ => "notChecked",
    };
}
