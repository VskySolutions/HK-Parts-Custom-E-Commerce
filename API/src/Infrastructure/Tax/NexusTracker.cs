using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Tax;

/// <summary>
/// US economic-nexus accumulator (REQ-TAX-004). On each completed US order it adds the order total and a
/// transaction to the destination state's running totals for the calendar year, and raises a single admin
/// alert when the state first crosses the warning fraction of its statutory threshold. Only runs when the
/// active provider is TaxJar (AC-TAX-004.4); statutory thresholds default to $100k / 200 transactions and
/// can be tuned per accumulator row.
/// </summary>
public class NexusTracker : INexusTracker
{
    private const decimal DefaultThresholdAmount = 100_000m;
    private const int DefaultThresholdTransactions = 200;
    private const decimal DefaultWarningPercent = 0.80m;

    private readonly IApplicationDbContext _db;
    private readonly IAdminAlertService _alerts;

    public NexusTracker(IApplicationDbContext db, IAdminAlertService alerts)
    {
        _db = db;
        _alerts = alerts;
    }

    public async Task TrackOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // Gated to TaxJar (AC-TAX-004.4): inert for FlatRate / Stripe.
        var activeProvider = await _db.TaxProviderConfigurations
            .AsNoTracking()
            .Select(c => c.ActiveProvider)
            .FirstOrDefaultAsync(cancellationToken);
        if (activeProvider != TaxProviderType.TaxJar)
            return;

        var order = await _db.Orders
            .AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.CountryCode, o.Region, o.TotalAmount, o.PlacedOnUtc })
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null
            || !string.Equals(order.CountryCode, "US", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(order.Region))
            return;

        var stateCode = order.Region!.Trim().ToUpperInvariant();
        var periodStart = new DateTime(order.PlacedOnUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var accumulator = await _db.StateNexusAccumulators
            .FirstOrDefaultAsync(a => a.StateCode == stateCode && a.PeriodStartUtc == periodStart, cancellationToken);

        if (accumulator is null)
        {
            accumulator = new StateNexusAccumulator
            {
                StateCode = stateCode,
                PeriodStartUtc = periodStart,
                ThresholdAmount = DefaultThresholdAmount,
                ThresholdTransactions = DefaultThresholdTransactions,
                WarningPercent = DefaultWarningPercent,
            };
            _db.StateNexusAccumulators.Add(accumulator);
        }

        accumulator.GrossSales += order.TotalAmount;
        accumulator.TransactionCount += 1;

        var revenueFraction = accumulator.ThresholdAmount > 0 ? accumulator.GrossSales / accumulator.ThresholdAmount : 0m;
        var countFraction = accumulator.ThresholdTransactions is int tt && tt > 0
            ? (decimal)accumulator.TransactionCount / tt
            : 0m;
        var approaching = revenueFraction >= accumulator.WarningPercent || countFraction >= accumulator.WarningPercent;

        // Alert once per accumulator (per state-year) when it first approaches the threshold.
        if (approaching && accumulator.LastAlertedAtUtc is null)
        {
            var title = $"Approaching economic nexus in {stateCode}";
            var message =
                $"{stateCode} {periodStart.Year}: gross sales {accumulator.GrossSales:C} of {accumulator.ThresholdAmount:C} " +
                $"({revenueFraction:P0}) and {accumulator.TransactionCount} of {accumulator.ThresholdTransactions} transactions. " +
                "Review whether tax registration is required.";
            await _alerts.RaiseAsync("NexusThreshold", title, message, "Warning", "NexusTracker", cancellationToken);
            accumulator.LastAlertedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
