using HKPartsEcommerce.Application.Features.PriceRules;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Pricing;

/// <summary>Pure price-rule arithmetic — no database.</summary>
public class PriceRuleMathTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Theory]
    [InlineData(100, PriceRuleDiscountType.Percent, 10, 90)]
    [InlineData(95, PriceRuleDiscountType.Percent, 10, 85.50)]
    [InlineData(120, PriceRuleDiscountType.Percent, 10, 108)]
    [InlineData(100, PriceRuleDiscountType.Percent, 100, 0)]
    [InlineData(100, PriceRuleDiscountType.FixedAmount, 15, 85)]
    [InlineData(100, PriceRuleDiscountType.FixedAmount, 150, 0)]
    // Half away from zero: 10.005 → 10.01 (banker's rounding would give 10.00).
    [InlineData(10.01, PriceRuleDiscountType.FixedAmount, 0.005, 10.01)]
    [InlineData(0.15, PriceRuleDiscountType.Percent, 50, 0.08)]
    public void ComputeRulePrice(decimal basePrice, PriceRuleDiscountType type, decimal amount, decimal expected) =>
        Assert.Equal(expected, PriceRuleMath.ComputeRulePrice(basePrice, type, amount));

    [Fact]
    public void Window_is_inclusive_and_open_ended()
    {
        Assert.True(PriceRuleMath.IsInWindow(Today, Today, Today));
        Assert.True(PriceRuleMath.IsInWindow(null, null, Today));
        Assert.False(PriceRuleMath.IsInWindow(Today.AddDays(1), null, Today));
        Assert.False(PriceRuleMath.IsInWindow(null, Today.AddDays(-1), Today));
    }

    private static RulePriceCandidate Row(Guid id, decimal price, int priority, bool stop = false,
        DateOnly? start = null, DateOnly? end = null) => new(id, price, start, end, priority, stop);

    [Fact]
    public void Lowest_of_walked_rules_wins_without_stacking()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var pick = PriceRuleMath.PickRulePrice(new[] { Row(a, 90m, 0), Row(b, 80m, 1) }, _ => Today);
        Assert.Equal((80m, b), pick);
    }

    [Fact]
    public void Stop_further_rules_ends_the_walk()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var pick = PriceRuleMath.PickRulePrice(new[] { Row(b, 80m, 1), Row(a, 90m, 0, stop: true) }, _ => Today);
        Assert.Equal((90m, a), pick);
    }

    [Fact]
    public void Rows_outside_their_window_are_skipped_before_the_walk()
    {
        // A future "stop" rule must not block a running lower-priority rule.
        var future = Guid.NewGuid();
        var running = Guid.NewGuid();
        var pick = PriceRuleMath.PickRulePrice(
            new[] { Row(future, 50m, 0, stop: true, start: Today.AddDays(1)), Row(running, 90m, 1) }, _ => Today);
        Assert.Equal((90m, running), pick);
    }

    [Fact]
    public void No_rows_no_rule() => Assert.Null(PriceRuleMath.PickRulePrice(Array.Empty<RulePriceCandidate>(), _ => Today));

    [Theory]
    [InlineData(100, -1, 90, 90, false, true)]
    [InlineData(100, 85, 90, 85, true, false)]
    [InlineData(100, 85, 80, 80, true, true)]
    [InlineData(100, -1, -1, 100, false, false)]
    // A rule never raises a price, and a "sale" above base is ignored.
    [InlineData(80, -1, 90, 80, false, false)]
    [InlineData(80, 90, -1, 80, false, false)]
    // -1 = none (attribute arguments cannot be nullable decimals).
    public void Combine_lowest_wins(int basePrice, int sale, int rule, int final, bool onSale, bool ruleApplied)
    {
        var result = PriceRuleMath.Combine(basePrice, sale < 0 ? null : sale, rule < 0 ? null : rule);
        Assert.Equal(((decimal)final, onSale, ruleApplied), result);
    }

    [Fact]
    public void Today_honours_the_store_time_zone()
    {
        // 2026-11-05 03:00 UTC is still Nov 4 in New York (UTC-5).
        var nowUtc = new DateTime(2026, 11, 5, 3, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 11, 4), PriceRuleMath.TodayIn("America/New_York", nowUtc));
        Assert.Equal(new DateOnly(2026, 11, 5), PriceRuleMath.TodayIn(null, nowUtc));
        Assert.Equal(new DateOnly(2026, 11, 5), PriceRuleMath.TodayIn("Not/AZone", nowUtc));
    }
}
