using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Checkout;

/// <summary>Pure rules behind the checkout settings: blacklist matching (PY-15), state notices (SH-23), validation.</summary>
public class CheckoutSettingsRulesTests
{
    private static readonly CustomerBlacklistEntry Entry = new() { FirstName = "John", LastName = "Smith", Email = "bad@example.com" };

    [Theory]
    [InlineData(BlacklistMatchMode.NameOrEmail, "john", "SMITH", "other@example.com", "Name")]
    [InlineData(BlacklistMatchMode.NameOrEmail, "Jane", "Doe", " BAD@example.com ", "Email")]
    [InlineData(BlacklistMatchMode.NameOrEmail, "John", "Smith", "bad@example.com", "Name+Email")]
    [InlineData(BlacklistMatchMode.EmailOnly, "John", "Smith", "other@example.com", null)]
    [InlineData(BlacklistMatchMode.EmailOnly, "Jane", "Doe", "bad@example.com", "Email")]
    [InlineData(BlacklistMatchMode.NameAndEmail, "John", "Smith", "other@example.com", null)]
    [InlineData(BlacklistMatchMode.NameAndEmail, "John", "Smith", "bad@example.com", "Name+Email")]
    [InlineData(BlacklistMatchMode.NameOrEmail, "John", null, null, null)]
    public void Blacklist_match_modes(BlacklistMatchMode mode, string? first, string? last, string? email, string? expected)
    {
        var match = CustomerBlacklistMatcher.Match(new[] { Entry }, first, last, email, mode);
        Assert.Equal(expected, match?.MatchedOn);
    }

    [Fact]
    public void Inactive_entries_never_match()
    {
        var inactive = new CustomerBlacklistEntry { Email = "bad@example.com", Status = CustomerBlacklistStatus.Inactive };
        Assert.Null(CustomerBlacklistMatcher.Match(new[] { inactive }, null, null, "bad@example.com", BlacklistMatchMode.NameOrEmail));
    }

    [Fact]
    public void State_notice_matches_code_or_full_name_and_skips_inactive()
    {
        var s = new CheckoutSettingsDto
        {
            StateNotices = new() { new("WA", "wa notice"), new("OR", "or notice", Active: false) },
        };
        Assert.Equal("wa notice", CheckoutSettingsStore.NoticeFor(s, "wa"));
        Assert.Equal("wa notice", CheckoutSettingsStore.NoticeFor(s, "Washington"));
        Assert.Null(CheckoutSettingsStore.NoticeFor(s, "OR"));
        Assert.Null(CheckoutSettingsStore.NoticeFor(s, null));
    }

    [Fact]
    public void Defaults_are_the_legacy_values()
    {
        var s = new CheckoutSettingsDto();
        Assert.Equal(1800, s.SessionTimeoutSeconds);
        Assert.True(s.LockEnabled);
        Assert.Equal(10, s.LockQtyThreshold);
        Assert.Equal(600, s.LockSeconds);
        Assert.True(s.TermsRequired);
        Assert.Equal("/privacy--terms", s.TermsUrl);
        Assert.Equal(BlacklistMatchMode.NameOrEmail, s.BlacklistMatchMode);
        Assert.Equal("WA", Assert.Single(s.StateNotices).State);
    }

    [Fact]
    public void Validator_rejects_bad_values()
    {
        var v = new UpdateCheckoutSettingsCommandValidator();
        var ok = new UpdateCheckoutSettingsCommand(1800, true, 10, 600, true, "Accept", "/terms",
            new() { new("WA", "x") }, BlacklistMatchMode.EmailOnly, "a@b.com, c@d.com");
        Assert.True(v.Validate(ok).IsValid);

        Assert.False(v.Validate(ok with { LockSeconds = 5 }).IsValid);
        Assert.False(v.Validate(ok with { TermsText = "" }).IsValid);
        Assert.False(v.Validate(ok with { BlacklistAlertEmail = "not-an-email" }).IsValid);
        Assert.False(v.Validate(ok with { StateNotices = new() { new("WA", "x"), new("Washington", "y") } }).IsValid);
    }

    [Fact]
    public void Blacklist_entry_needs_an_email_or_a_full_name()
    {
        var v = new SaveCustomerBlacklistEntryCommandValidator();
        Assert.False(v.Validate(new SaveCustomerBlacklistEntryCommand(null, "John", null, null, null)).IsValid);
        Assert.True(v.Validate(new SaveCustomerBlacklistEntryCommand(null, "John", "Smith", null, null)).IsValid);
        Assert.True(v.Validate(new SaveCustomerBlacklistEntryCommand(null, null, null, "x@y.com", null)).IsValid);
    }
}
