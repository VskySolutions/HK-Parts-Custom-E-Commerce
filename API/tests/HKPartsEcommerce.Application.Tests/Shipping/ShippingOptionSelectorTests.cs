using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Shipping;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Shipping;

/// <summary>
/// Best-value scoring for the automatic shipping recommendation (REQ-SHP-006). Cost and delivery days are
/// min-max normalised across the offered set and combined with the merchant's cost-vs-speed weight.
/// </summary>
public class ShippingOptionSelectorTests : CatalogTestBase
{
    private static ShippingRateOption Option(string id, decimal rate, int? days) =>
        new(MethodId: id, Name: id, Carrier: "Test", EstimatedDeliveryDays: days, Rate: rate);

    private void SeedConfig(
        ShippingSelectionMode mode = ShippingSelectionMode.Automatic,
        int weight = 50,
        int assumedDays = 7)
    {
        using var db = NewContext();
        db.ShippingProviderConfigurations.Add(new ShippingProviderConfiguration
        {
            IsEnabled = true,
            SelectionMode = mode,
            CostVsSpeedWeight = weight,
            AssumedTransitDays = assumedDays,
        });
        db.SaveChanges();
    }

    private async Task<ShippingSelectionResult> SelectAsync(params ShippingRateOption[] options)
    {
        using var db = NewContext();
        var selector = new ShippingOptionSelector(db);
        return await selector.SelectAsync(options, CancellationToken.None);
    }

    [Fact]
    public async Task Manual_recommends_nothing()
    {
        SeedConfig(ShippingSelectionMode.Manual);

        var result = await SelectAsync(Option("cheap", 5m, 5), Option("fast", 22m, 1));

        Assert.Null(result.Recommended);
        Assert.All(result.Options, o => Assert.False(o.IsRecommended));
    }

    [Fact]
    public async Task No_configuration_row_defaults_to_manual()
    {
        var result = await SelectAsync(Option("cheap", 5m, 5), Option("fast", 22m, 1));

        Assert.Null(result.Recommended);
    }

    [Fact]
    public async Task Weight_100_picks_the_cheapest()
    {
        SeedConfig(weight: 100);

        var result = await SelectAsync(Option("cheap", 5m, 9), Option("mid", 8m, 4), Option("fast", 22m, 1));

        Assert.Equal("cheap", result.Recommended!.MethodId);
    }

    [Fact]
    public async Task Weight_0_picks_the_fastest()
    {
        SeedConfig(weight: 0);

        var result = await SelectAsync(Option("cheap", 5m, 9), Option("mid", 8m, 4), Option("fast", 22m, 1));

        Assert.Equal("fast", result.Recommended!.MethodId);
    }

    [Fact]
    public async Task Balanced_prefers_the_middle_option_over_both_extremes()
    {
        SeedConfig(weight: 50);

        // Rates span 5..22 (range 17), days span 1..9 (range 8):
        // cheap: norm(rate)=0.000 norm(days)=1.000 -> 0.500
        // mid:   norm(rate)=0.176 norm(days)=0.375 -> 0.276  <- best value
        // fast:  norm(rate)=1.000 norm(days)=0.000 -> 0.500
        var result = await SelectAsync(Option("cheap", 5m, 9), Option("mid", 8m, 4), Option("fast", 22m, 1));

        Assert.Equal("mid", result.Recommended!.MethodId);
    }

    [Fact]
    public async Task Only_one_option_is_flagged()
    {
        SeedConfig();

        var result = await SelectAsync(Option("a", 5m, 5), Option("b", 8m, 3), Option("c", 22m, 1));

        Assert.Single(result.Options, o => o.IsRecommended);
        Assert.True(result.Recommended!.IsRecommended);
    }

    [Fact]
    public async Task Unknown_delivery_days_are_scored_as_the_assumed_value_not_as_instant()
    {
        // Speed-only weighting: were the null treated as 0 days it would beat the known 2-day option.
        SeedConfig(weight: 0, assumedDays: 10);

        var result = await SelectAsync(Option("unknown", 5m, null), Option("known", 20m, 2));

        Assert.Equal("known", result.Recommended!.MethodId);
    }

    [Fact]
    public async Task Identical_options_do_not_divide_by_zero()
    {
        SeedConfig(weight: 50);

        var result = await SelectAsync(Option("a", 10m, 3), Option("b", 10m, 3));

        Assert.NotNull(result.Recommended);
        Assert.Single(result.Options, o => o.IsRecommended);
    }

    [Fact]
    public async Task Ties_break_toward_the_cheaper_option()
    {
        // Symmetric pair: both score 0.5 at balanced weighting, so the cheaper one must win.
        SeedConfig(weight: 50);

        var result = await SelectAsync(Option("dear", 20m, 1), Option("cheap", 5m, 9));

        Assert.Equal("cheap", result.Recommended!.MethodId);
    }

    [Fact]
    public async Task Empty_input_recommends_nothing()
    {
        SeedConfig();

        var result = await SelectAsync();

        Assert.Empty(result.Options);
        Assert.Null(result.Recommended);
    }

    [Fact]
    public async Task Single_option_is_recommended()
    {
        SeedConfig();

        var result = await SelectAsync(Option("only", 9m, 3));

        Assert.Equal("only", result.Recommended!.MethodId);
    }
}
