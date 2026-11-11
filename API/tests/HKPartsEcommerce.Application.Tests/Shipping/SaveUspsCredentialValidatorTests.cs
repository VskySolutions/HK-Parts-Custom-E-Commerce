using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>The USPS credential form's save rules (docs/usps-shipping-requirements.md §4, US-T2, US-52).</summary>
public class SaveUspsCredentialValidatorTests
{
    private readonly SaveUspsCredentialCommandValidator _validator = new();

    /// <summary>Exactly what the admin "Add USPS credential" form posts with its prefilled defaults.</summary>
    private static SaveUspsCredentialCommand DefaultForm(bool active = true) => new(
        null, "Test account", active, false,
        "https://apis-tem.usps.com", "key", "secret",
        DisplayName: "USPS",
        PriceType: "RETAIL",
        AllowedDomesticServices: new() { "USPS_GROUND_ADVANTAGE", "PRIORITY_MAIL", "PRIORITY_MAIL_EXPRESS" },
        AllowedInternationalServices: new() { "PRIORITY_MAIL_INTERNATIONAL", "PRIORITY_MAIL_EXPRESS_INTERNATIONAL", "FIRST-CLASS_PACKAGE_INTERNATIONAL_SERVICE" },
        ShipToCountries: new() { "US" });

    [Fact]
    public void Default_form_is_valid()
        => Assert.True(_validator.Validate(DefaultForm()).IsValid);

    [Fact]
    public void Http_base_url_is_refused()
    {
        var result = _validator.Validate(DefaultForm() with { BaseUrl = "http://apis-tem.usps.com" });
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("HTTPS"));
    }

    [Fact]
    public void US_T2_active_with_no_domestic_service_is_refused_inactive_is_allowed()
    {
        Assert.False(_validator.Validate(DefaultForm() with { AllowedDomesticServices = new() }).IsValid);
        Assert.True(_validator.Validate(DefaultForm(active: false) with { AllowedDomesticServices = new() }).IsValid);
    }

    [Fact]
    public void Country_names_are_refused_iso_codes_accepted()
    {
        Assert.False(_validator.Validate(DefaultForm() with { ShipToCountries = new() { "United States" } }).IsValid);
        Assert.True(_validator.Validate(DefaultForm() with { ShipToCountries = new() { "US", "CA" } }).IsValid);
    }
}
