using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.PriceRules;

/// <summary>A flat price-rule row as the resolver sees it (already narrowed to one product/variant + group).</summary>
public readonly record struct RulePriceCandidate(
    Guid PriceRuleId, decimal RulePrice, DateOnly? StartDate, DateOnly? EndDate, int Priority, bool StopFurtherRules);

/// <summary>
/// The pure pricing arithmetic, shared by the resolver, the preview and the unit tests. The set-based rebuild
/// SQL mirrors <see cref="ComputeRulePrice"/> exactly (SQL Server <c>ROUND</c> on decimals rounds half away
/// from zero).
/// </summary>
public static class PriceRuleMath
{
    /// <summary>
    /// Percent: base − base × amount / 100. Fixed: max(0, base − amount). Rounded to 2 decimals, half away
    /// from zero.
    /// </summary>
    public static decimal ComputeRulePrice(decimal basePrice, PriceRuleDiscountType type, decimal amount)
    {
        var raw = type == PriceRuleDiscountType.Percent
            ? basePrice - basePrice * amount / 100m
            : Math.Max(0m, basePrice - amount);
        return Math.Round(Math.Max(0m, raw), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Inclusive date window; a null bound is open.</summary>
    public static bool IsInWindow(DateOnly? start, DateOnly? end, DateOnly today) =>
        (start is null || start.Value <= today) && (end is null || end.Value >= today);

    /// <summary>
    /// The winning rule price: rows in force today, ordered by Priority then PriceRuleId, walked until (and
    /// including) the first with StopFurtherRules; the lowest price among those walked wins. Rules never stack.
    /// </summary>
    /// <param name="todayFor">Today's date in the rule's store time zone.</param>
    public static (decimal Price, Guid RuleId)? PickRulePrice(
        IEnumerable<RulePriceCandidate> candidates, Func<RulePriceCandidate, DateOnly> todayFor)
    {
        (decimal Price, Guid RuleId)? best = null;
        foreach (var row in candidates
                     .Where(c => IsInWindow(c.StartDate, c.EndDate, todayFor(c)))
                     .OrderBy(c => c.Priority)
                     .ThenBy(c => c.PriceRuleId))
        {
            if (best is null || row.RulePrice < best.Value.Price)
                best = (row.RulePrice, row.PriceRuleId);
            if (row.StopFurtherRules)
                break;
        }

        return best;
    }

    /// <summary>
    /// Combines base, sale and rule: the lowest wins, and neither a sale nor a rule ever raises the price.
    /// </summary>
    public static (decimal Final, bool OnSale, bool RuleApplied) Combine(
        decimal basePrice, decimal? activeSalePrice, decimal? rulePrice)
    {
        var final = basePrice;
        var onSale = false;
        if (activeSalePrice is decimal sale && sale <= final)
        {
            final = sale;
            onSale = true;
        }

        var ruleApplied = false;
        if (rulePrice is decimal rule && rule < final)
        {
            final = rule;
            ruleApplied = true;
        }

        return (final, onSale, ruleApplied);
    }

    /// <summary>Today's date in a time zone (IANA or Windows id); unknown/blank ids fall back to UTC.</summary>
    public static DateOnly TodayIn(string? timeZoneId, DateTime nowUtc) =>
        DateOnly.FromDateTime(ToLocal(timeZoneId, nowUtc));

    public static DateTime ToLocal(string? timeZoneId, DateTime nowUtc)
    {
        var utc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return utc;
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return utc;
        }
    }
}
