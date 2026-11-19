using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.AddressVerification;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.AddressVerification;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>
/// Checkout address verification (docs/shipstation-verification-requirements.md §4, §7 AV-T*). The provider is
/// a fake; the USPS / Avalara answer readers are tested on sample payloads.
/// </summary>
public class AddressVerificationTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private static readonly CheckoutAddress Valid =
        new("Dana", "Reed", "dana@example.test", "3334 Gray Moss Rd", null, "Charlotte", "NC", "28270", "US");

    private sealed class FakeProvider : IAddressVerificationProvider
    {
        public int Calls;
        public AddressVerificationStatus Next = AddressVerificationStatus.Valid;
        public VerifiableAddress? Suggestion;
        public AddressVerificationProvider Provider => AddressVerificationProvider.Usps;
        public Task<bool> IsConfiguredAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<AddressVerificationOutcome> VerifyAsync(VerifiableAddress address, bool includeRaw, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var outcome = new AddressVerificationOutcome { Status = Next, Suggestion = Suggestion };
            if (Next == AddressVerificationStatus.Invalid) outcome.Messages.Add("Address Not Found.");
            return Task.FromResult(outcome);
        }
    }

    private async Task SeedSettingsAsync(
        AddressVerificationProvider provider = AddressVerificationProvider.Usps, string? countries = "US",
        AddressUnavailablePolicy onUnavailable = AddressUnavailablePolicy.AllowAndFlag, bool blockPoBox = false)
    {
        await using var db = NewContext();
        db.AddressVerificationSettings.Add(new AddressVerificationSettings
        {
            Provider = provider, Countries = countries, FailedMessage = "Please check your address.", OnUnavailable = onUnavailable,
        });
        if (blockPoBox)
            db.ShippingRuleSettings.Add(new ShippingRuleSettings { RefusePoBoxes = true, PoBoxMessage = "No P.O. boxes." });
        await db.SaveChangesAsync();
    }

    private static AddressVerificationService Service(IApplicationDbContext ctx, FakeProvider provider) =>
        new(ctx, new[] { provider }, new FixedClock(Now), NullLogger<AddressVerificationService>.Instance);

    private async Task<CheckoutAddressCheck> CheckAsync(FakeProvider provider, CheckoutAddress address)
    {
        await using var ctx = NewContext();
        return await CheckoutAddressGate.CheckAsync(ctx, Service(ctx, provider), address, default);
    }

    [Fact]
    public async Task AvT1_T7_valid_address_is_verified_once()
    {
        await SeedSettingsAsync();
        var provider = new FakeProvider();

        var first = await CheckAsync(provider, Valid);
        // Same address, different case and punctuation: still the cached answer (AV-06).
        var second = await CheckAsync(provider, Valid with { Line1 = "3334 GRAY MOSS RD." });

        Assert.Equal(AddressVerificationStatus.Valid, first.Status);
        Assert.False(first.Blocking);
        Assert.Equal(AddressVerificationStatus.Valid, second.Status);
        Assert.Equal(1, provider.Calls);
    }

    [Fact]
    public async Task AvT2_invalid_address_blocks_with_the_admin_message_and_reason()
    {
        await SeedSettingsAsync();
        var check = await CheckAsync(new FakeProvider { Next = AddressVerificationStatus.Invalid }, Valid with { Line1 = "123 Fake Street" });

        Assert.True(check.Blocking);
        Assert.Equal(AddressVerificationCodes.Invalid, check.Code);
        Assert.Equal("Please check your address.", check.Message);
        Assert.Contains("Address Not Found.", check.Messages);
    }

    [Fact]
    public async Task AvT4_provider_down_lets_the_order_continue_unless_blocking_is_chosen()
    {
        await SeedSettingsAsync();
        var down = new FakeProvider { Next = AddressVerificationStatus.Unavailable };

        var check = await CheckAsync(down, Valid);
        Assert.Equal(AddressVerificationStatus.Unavailable, check.Status);
        Assert.False(check.Blocking);

        // Unavailable is never cached: the next attempt asks again.
        await CheckAsync(down, Valid);
        Assert.Equal(2, down.Calls);
    }

    [Fact]
    public async Task AvT4b_provider_down_blocks_when_the_admin_chose_block()
    {
        await SeedSettingsAsync(onUnavailable: AddressUnavailablePolicy.Block);
        var check = await CheckAsync(new FakeProvider { Next = AddressVerificationStatus.Unavailable }, Valid);
        Assert.True(check.Blocking);
        Assert.Equal(AddressVerificationCodes.Unavailable, check.Code);
    }

    [Fact]
    public async Task AvT5_country_not_listed_is_not_verified()
    {
        await SeedSettingsAsync(provider: AddressVerificationProvider.Usps, countries: "US");
        var provider = new FakeProvider();
        var check = await CheckAsync(provider, Valid with { CountryCode = "CA", Region = "ON", PostalCode = "M5V 2T6", City = "Toronto" });
        Assert.Equal(AddressVerificationStatus.NotChecked, check.Status);
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task PoT3_po_box_is_refused_even_with_verification_off()
    {
        await SeedSettingsAsync(provider: AddressVerificationProvider.None, blockPoBox: true);
        var check = await CheckAsync(new FakeProvider(), Valid with { Line1 = "PO Box 12" });
        Assert.True(check.Blocking);
        Assert.Equal(AddressVerificationCodes.PoBox, check.Code);
        Assert.Equal("No P.O. boxes.", check.Message);
    }

    [Fact]
    public async Task AvT3_suggestion_is_offered_and_keep_mine_is_recorded_on_the_cart()
    {
        await SeedSettingsAsync();
        var provider = new FakeProvider
        {
            Next = AddressVerificationStatus.Suggested,
            Suggestion = new VerifiableAddress("3334 GRAY MOSS RD", null, "CHARLOTTE", "NC", "28270-1234", "US"),
        };
        await using (var db = NewContext())
        {
            db.Carts.Add(new Domain.Entities.Cart { SessionId = "sess-av-3" });
            await db.SaveChangesAsync();
        }
        var entered = Valid with { Line1 = "3334 gray moss road" };

        await using (var ctx = NewContext())
        {
            var dto = await new VerifyCheckoutAddressCommandHandler(ctx, Service(ctx, provider), new FakeCurrentUser())
                .Handle(new VerifyCheckoutAddressCommand(null, "sess-av-3", entered), default);
            Assert.Equal("suggested", dto.Status);
            Assert.Equal("3334 GRAY MOSS RD", dto.Suggestion!.Line1);
        }

        await using (var ctx = NewContext())
        {
            await new AcceptCheckoutAddressCommandHandler(ctx, Service(ctx, provider), new FakeCurrentUser())
                .Handle(new AcceptCheckoutAddressCommand(null, "sess-av-3", entered, KeepMine: true), default);
        }

        await using var check = NewContext();
        var cart = await check.Carts.SingleAsync(c => c.SessionId == "sess-av-3");
        Assert.True(cart.ShipToAddressOverridden);
        Assert.Equal(AddressVerificationStatus.Suggested, cart.ShipToVerifyStatus);
        Assert.Equal(AddressVerificationSupport.Hash(AddressVerificationSupport.ToVerifiable(entered)), cart.ShipToAddressHash);
    }

    [Fact]
    public void AvT6_T25_settings_validator()
    {
        var validator = new UpdateAddressVerificationSettingsCommandValidator();
        var noCountries = new UpdateAddressVerificationSettingsCommand(
            AddressVerificationProvider.Avalara, "Check it", new List<string>(), true, AddressUnavailablePolicy.AllowAndFlag, false, false, null);
        Assert.False(validator.Validate(noCountries).IsValid);

        var uspsCanada = noCountries with { Provider = AddressVerificationProvider.Usps, Countries = new List<string> { "US", "CA" } };
        Assert.False(validator.Validate(uspsCanada).IsValid);

        var ok = noCountries with { Provider = AddressVerificationProvider.Usps, Countries = new List<string> { "US" } };
        Assert.True(validator.Validate(ok).IsValid);
    }

    // ---------------------------------------------------------------- provider answers

    [Fact]
    public void Usps_answer_with_corrections_is_a_suggestion()
    {
        const string body = """
        {"firm":"","address":{"streetAddress":"3334 GRAY MOSS RD","secondaryAddress":"","city":"CHARLOTTE","state":"NC","ZIPCode":"28270","ZIPPlus4":"1234"},
         "additionalInfo":{"DPVConfirmation":"Y","business":"N"},"corrections":[{"code":"","text":""}],"matches":[{"code":"31","text":"Single Response - exact match"}]}
        """;
        var outcome = UspsAddressProvider.Interpret(
            new VerifiableAddress("3334 gray moss road", null, "charlotte", "North Carolina", "28270", "US"), body);

        Assert.Equal(AddressVerificationStatus.Suggested, outcome.Status);
        Assert.Equal("28270-1234", outcome.Suggestion!.PostalCode);
        Assert.True(outcome.IsResidential);
    }

    [Fact]
    public void Usps_answer_matching_the_input_except_zip4_is_valid()
    {
        const string body = """
        {"address":{"streetAddress":"3334 GRAY MOSS RD","city":"CHARLOTTE","state":"NC","ZIPCode":"28270","ZIPPlus4":"1234"},"additionalInfo":{"DPVConfirmation":"Y"}}
        """;
        var outcome = UspsAddressProvider.Interpret(
            new VerifiableAddress("3334 Gray Moss Rd", null, "Charlotte", "NC", "28270", "US"), body);
        Assert.Equal(AddressVerificationStatus.Valid, outcome.Status);
    }

    [Fact]
    public void Usps_undeliverable_answer_is_invalid()
    {
        const string body = """{"address":{"streetAddress":"123 FAKE ST","city":"CHARLOTTE","state":"NC","ZIPCode":"28270"},"additionalInfo":{"DPVConfirmation":"N"}}""";
        var outcome = UspsAddressProvider.Interpret(new VerifiableAddress("123 Fake Street", null, "Charlotte", "NC", "28270", "US"), body);
        Assert.Equal(AddressVerificationStatus.Invalid, outcome.Status);
    }

    [Fact]
    public void AvT10_avalara_resolved_address_passes_and_errors_fail()
    {
        const string ok = """
        {"address":{"line1":"3334 Gray Moss Rd"},"validatedAddresses":[{"addressType":"StreetOrResidentialAddress","line1":"3334 GRAY MOSS RD","line2":"","city":"CHARLOTTE","region":"NC","country":"US","postalCode":"28270-1234"}],"resolutionQuality":"Rooftop"}
        """;
        Assert.Equal(AddressVerificationStatus.Valid, AvalaraAddressProvider.Interpret(
            new VerifiableAddress("3334 Gray Moss Rd", null, "Charlotte", "NC", "28270", "US"), ok).Status);

        const string bad = """
        {"address":{"line1":"123 Fake St"},"validatedAddresses":[{"line1":"123 Fake St"}],"messages":[{"summary":"The address is not deliverable.","severity":"Error"}]}
        """;
        var outcome = AvalaraAddressProvider.Interpret(new VerifiableAddress("123 Fake St", null, "Charlotte", "NC", "28270", "US"), bad);
        Assert.Equal(AddressVerificationStatus.Invalid, outcome.Status);
        Assert.Contains("The address is not deliverable.", outcome.Messages);
    }
}
