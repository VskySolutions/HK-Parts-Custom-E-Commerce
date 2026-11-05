using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.PriceRules;

/// <summary>
/// Rule dates are compared in the store's time zone. A rule with no store (all stores) uses the
/// <em>primary</em> store's zone — the first-created enabled store — and UTC when no store exists.
/// </summary>
public sealed class PriceRuleClock
{
    private readonly IReadOnlyDictionary<Guid, string> _zones;
    private readonly string? _defaultZone;

    private PriceRuleClock(IReadOnlyDictionary<Guid, string> zones, string? defaultZone)
    {
        _zones = zones;
        _defaultZone = defaultZone;
    }

    /// <summary>The store table is tiny, so one query loads every zone.</summary>
    public static async Task<PriceRuleClock> LoadAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var stores = await db.Stores.AsNoTracking()
            .OrderBy(s => s.CreatedOnUtc)
            .Select(s => new { s.Id, s.TimeZone, s.IsEnabled })
            .ToListAsync(ct);

        return new PriceRuleClock(
            stores.ToDictionary(s => s.Id, s => s.TimeZone),
            stores.FirstOrDefault(s => s.IsEnabled)?.TimeZone ?? stores.FirstOrDefault()?.TimeZone);
    }

    public string? ZoneFor(Guid? storeId) =>
        storeId is Guid id && _zones.TryGetValue(id, out var zone) ? zone : _defaultZone;

    public DateOnly TodayFor(Guid? storeId, DateTime nowUtc) => PriceRuleMath.TodayIn(ZoneFor(storeId), nowUtc);

    public DateTime LocalNowFor(Guid? storeId, DateTime nowUtc) => PriceRuleMath.ToLocal(ZoneFor(storeId), nowUtc);
}
