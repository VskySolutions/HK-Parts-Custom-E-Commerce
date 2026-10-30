using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Shipping;

/// <summary>
/// Scores the offered shipping options and recommends the best value (REQ-SHP-006).
///
/// Cost and delivery time are on different scales and in different units, so each is min-max normalised to
/// 0..1 across the options actually on offer and combined with the merchant's cost-vs-speed weight. The
/// lowest score wins. Normalising against the offered set (rather than absolute thresholds) keeps the
/// recommendation sensible whether the options span $5–$8 or $5–$200.
/// </summary>
public class ShippingOptionSelector : IShippingOptionSelector
{
    private readonly IApplicationDbContext _db;

    public ShippingOptionSelector(IApplicationDbContext db) => _db = db;

    public async Task<ShippingSelectionResult> SelectAsync(IReadOnlyList<ShippingRateOption> options, CancellationToken ct)
    {
        if (options.Count == 0)
            return new ShippingSelectionResult(options, null);

        var config = await _db.ShippingProviderConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(ct)
            ?? new ShippingProviderConfiguration();

        if (config.SelectionMode != ShippingSelectionMode.Automatic)
        {
            // Manual: nobody is recommended. Clear any stale flag so the contract holds regardless of what
            // the rate sources produced.
            var plain = options.Select(o => o with { IsRecommended = false }).ToList();
            return new ShippingSelectionResult(plain, null);
        }

        var winner = ScoreBest(options, config);
        var flagged = options.Select(o => o with { IsRecommended = ReferenceEquals(o, winner) }).ToList();

        // Return the flagged instance, not the original, so callers comparing by reference or reading
        // IsRecommended off the recommendation both see the same thing.
        return new ShippingSelectionResult(flagged, flagged.FirstOrDefault(o => o.IsRecommended));
    }

    private static ShippingRateOption ScoreBest(IReadOnlyList<ShippingRateOption> options, ShippingProviderConfiguration config)
    {
        var weight = Math.Clamp(config.CostVsSpeedWeight, 0, 100) / 100m;

        var rates = options.Select(o => o.Rate).ToList();
        var days = options.Select(o => (decimal)DaysOf(o, config)).ToList();

        var minRate = rates.Min();
        var maxRate = rates.Max();
        var minDays = days.Min();
        var maxDays = days.Max();

        ShippingRateOption? best = null;
        var bestScore = decimal.MaxValue;

        for (var i = 0; i < options.Count; i++)
        {
            var score = weight * Normalise(rates[i], minRate, maxRate)
                        + (1m - weight) * Normalise(days[i], minDays, maxDays);

            // Ties break toward the cheaper option, then the faster one — deterministic, and the
            // cheaper-on-a-tie default is the one a merchant is least likely to be surprised by.
            if (best is null
                || score < bestScore
                || (score == bestScore && (rates[i] < best.Rate
                    || (rates[i] == best.Rate && days[i] < DaysOf(best, config)))))
            {
                best = options[i];
                bestScore = score;
            }
        }

        return best!;
    }

    /// <summary>
    /// Min-max normalisation to 0..1. When every option shares a value the range is zero and that dimension
    /// carries no information, so it contributes 0 rather than dividing by zero.
    /// </summary>
    private static decimal Normalise(decimal value, decimal min, decimal max)
        => max <= min ? 0m : (value - min) / (max - min);

    /// <summary>
    /// An option with no estimate is treated as the configured assumed transit time. Without this an
    /// unknown option would normalise as though it were the fastest and win every scoring round.
    /// </summary>
    private static int DaysOf(ShippingRateOption option, ShippingProviderConfiguration config)
        => option.EstimatedDeliveryDays ?? config.AssumedTransitDays;
}
