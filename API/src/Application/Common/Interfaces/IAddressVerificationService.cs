using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>A postal address to verify: lines, city, region (state), postal code and ISO-2 country.</summary>
public record VerifiableAddress(
    string? Line1,
    string? Line2,
    string? City,
    string? Region,
    string? PostalCode,
    string? CountryCode);

/// <summary>What a provider (or the cache) said about one address.</summary>
public class AddressVerificationOutcome
{
    public AddressVerificationStatus Status { get; set; }
    public AddressVerificationProvider Provider { get; set; }

    /// <summary>The provider's standardised address (Suggested, and Valid when the provider returned one).</summary>
    public VerifiableAddress? Suggestion { get; set; }

    /// <summary>The provider's reasons ("Address not found", "Invalid ZIP for city"…) (AV-11).</summary>
    public List<string> Messages { get; set; } = new();

    public bool IsPoBox { get; set; }
    public bool? IsResidential { get; set; }
    public string AddressHash { get; set; } = string.Empty;
    public bool FromCache { get; set; }

    /// <summary>The raw provider response, only for the admin "Test address" box (AD-61). Never cached.</summary>
    public string? RawResponse { get; set; }
}

/// <summary>
/// Address verification (docs/shipstation-verification-requirements.md §4, §6.2): one entry point over the USPS
/// Addresses API and Avalara AvaTax v2. A provider failure never throws: it comes back as
/// <see cref="AddressVerificationStatus.Unavailable"/> so checkout can apply the "provider down" policy (AV-14).
/// </summary>
public interface IAddressVerificationService
{
    /// <summary>
    /// Verifies <paramref name="address"/> with the configured provider (or <paramref name="provider"/> when
    /// given — the admin test). <paramref name="checkoutRules"/> applies the country list (AV-02) and returns
    /// <see cref="AddressVerificationStatus.NotChecked"/> when verification is off or the country is not listed.
    /// Answers are cached by address hash for 30 days unless <paramref name="bypassCache"/>.
    /// </summary>
    Task<AddressVerificationOutcome> VerifyAsync(
        VerifiableAddress address,
        CancellationToken ct,
        bool checkoutRules = true,
        AddressVerificationProvider? provider = null,
        bool bypassCache = false,
        bool includeRaw = false);

    /// <summary>Whether the provider has an active credential (AD-62).</summary>
    Task<bool> IsProviderConfiguredAsync(AddressVerificationProvider provider, CancellationToken ct);
}
