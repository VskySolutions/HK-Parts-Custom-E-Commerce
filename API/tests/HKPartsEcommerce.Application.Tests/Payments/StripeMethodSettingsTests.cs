using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
using HKPartsEcommerce.Application.Tests.GiftCertificates;
using HKPartsEcommerce.Domain.Entities;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Payments;

/// <summary>
/// PHP parity, the Stripe method settings: minimum / maximum order amount and allowed customer groups decide whether
/// Stripe is offered at checkout, sort order places it among the methods, and the admin save keeps every field.
/// </summary>
public class StripeMethodSettingsTests : GiftCertificateTestBase
{
    private static string Session() => "s-" + Guid.NewGuid().ToString("N");

    private void SeedStripe(Action<StripeCredential> configure)
    {
        using var db = NewContext();
        var row = new StripeCredential { Name = "Stripe", Active = true, SecretKey = "sk_test_x", ReturnUrl = "https://shop.test/checkout" };
        configure(row);
        db.StripeCredentials.Add(row);
        db.SaveChanges();
    }

    private async Task<List<string>> MethodsFor(decimal price)
    {
        var session = Session();
        await AddToCart(session, SeedSellable(price));
        await using var db = NewContext();
        var quote = await Orchestrator(db).QuoteAsync(new CheckoutQuoteRequest(null, session, ShipTo, null), default);
        return quote.AvailablePaymentMethods.Select(m => m.Method).ToList();
    }

    [Fact]
    public async Task Stripe_is_offered_only_between_its_minimum_and_maximum()
    {
        SeedStripe(s => { s.MinOrderAmount = 50m; s.MaxOrderAmount = 100m; });

        Assert.DoesNotContain("Stripe", await MethodsFor(20m));
        Assert.Contains("Stripe", await MethodsFor(75m));
        Assert.DoesNotContain("Stripe", await MethodsFor(150m));
    }

    [Fact]
    public async Task Stripe_limited_to_a_customer_group_is_hidden_from_everyone_else()
    {
        SeedStripe(s => s.AllowedCustomerGroupIds = Guid.NewGuid().ToString());
        Assert.DoesNotContain("Stripe", await MethodsFor(20m)); // a guest is in no group
    }

    [Fact]
    public async Task Sort_order_moves_stripe_after_the_other_methods()
    {
        SeedStripe(s => s.SortOrder = 0);
        Assert.Equal(new[] { "Stripe", "BankTransfer" }, (await MethodsFor(20m)).ToArray());

        await using (var db = NewContext())
        {
            var row = db.StripeCredentials.Single();
            row.SortOrder = 5;
            db.SaveChanges();
        }
        Assert.Equal(new[] { "BankTransfer", "Stripe" }, (await MethodsFor(20m)).ToArray());
    }

    [Fact]
    public async Task Admin_save_keeps_every_stripe_method_setting()
    {
        var group = Guid.NewGuid();
        await using var db = NewContext();
        var dto = await new SaveStripeCredentialCommandHandler(db).Handle(new SaveStripeCredentialCommand(
            null, "Live Stripe", true, true, "pk_test", "sk_test", "https://shop.test/checkout", 2.5m,
            TransactionType: "AuthOnly", CustomerProfilesEnabled: true,
            AcceptedCardTypes: new List<string> { "visa", "MasterCard" },
            MinOrderAmount: 10m, MaxOrderAmount: 500m,
            AllowedCustomerGroupIds: new List<Guid> { group },
            DefaultOrderStatus: "Processing", SortOrder: null), default);

        Assert.Equal("AuthOnly", dto.TransactionType);
        Assert.True(dto.CustomerProfilesEnabled);
        Assert.Equal(new[] { "Visa", "MasterCard" }, dto.AcceptedCardTypes.ToArray());
        Assert.Equal(10m, dto.MinOrderAmount);
        Assert.Equal(500m, dto.MaxOrderAmount);
        Assert.Equal(group, Assert.Single(dto.AllowedCustomerGroupIds));
        Assert.Equal("Processing", dto.DefaultOrderStatus);
        Assert.Equal(0, dto.SortOrder);

        var validator = new SaveStripeCredentialCommandValidator();
        Assert.False(validator.Validate(new SaveStripeCredentialCommand(null, "x", true, false, null, "sk", null,
            AcceptedCardTypes: new List<string> { "Maestro" })).IsValid);
        Assert.False(validator.Validate(new SaveStripeCredentialCommand(null, "x", true, false, null, "sk", null,
            MinOrderAmount: 100m, MaxOrderAmount: 50m)).IsValid);
    }
}
