using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Singleton "Address Verification" settings (docs/shipstation-verification-requirements.md §5.2, replacing the
/// legacy <c>address_verification</c> / <c>verification_*</c> settings rows). The P.O. box rule shown on the
/// same admin screen stays on <see cref="ShippingRuleSettings"/>, because the destination rules that enforce it
/// at quote and placement already read it from there (AV-31: it runs even with verification off).
/// Provider credentials stay on their own screens (AD-62): USPS under shipping credentials, Avalara under tax.
/// </summary>
public class AddressVerificationSettings : AuditableEntity
{
    public AddressVerificationProvider Provider { get; set; } = AddressVerificationProvider.None;

    /// <summary>Shown to the buyer above the provider's reason when an address fails (AV-11).</summary>
    public string? FailedMessage { get; set; }

    /// <summary>Comma-separated ISO-2 countries whose ship-to addresses are verified (AV-02).</summary>
    public string? Countries { get; set; }

    /// <summary>Offer the provider's corrected address ("We suggest: …") (AV-13).</summary>
    public bool OfferSuggestion { get; set; } = true;

    public AddressUnavailablePolicy OnUnavailable { get; set; } = AddressUnavailablePolicy.AllowAndFlag;

    /// <summary>Log each provider call (address hash, status, duration; never the credentials).</summary>
    public bool LoggingEnabled { get; set; }
}

/// <summary>
/// One provider answer, cached by the hash of the normalised address for 30 days (§6.2), so an address is
/// verified once per change (AV-06) rather than on every visit to the checkout. Rows older than 30 days are
/// purged when new ones are written (NF-03: customer PII kept at most 30 days).
/// </summary>
public class AddressVerificationResult : BaseEntity
{
    /// <summary>SHA-256 (hex) of the normalised address lines, city, region, postal code and country.</summary>
    public string AddressHash { get; set; } = string.Empty;
    public AddressVerificationProvider Provider { get; set; }
    public AddressVerificationStatus Status { get; set; }
    public bool IsPoBox { get; set; }
    public bool? IsResidential { get; set; }

    /// <summary>The provider's standardised address as JSON (Line1, Line2, City, Region, PostalCode, CountryCode).</summary>
    public string? SuggestedJson { get; set; }

    /// <summary>The provider's reasons, newline-separated.</summary>
    public string? Messages { get; set; }

    public DateTime CreatedOnUtc { get; set; }
}
