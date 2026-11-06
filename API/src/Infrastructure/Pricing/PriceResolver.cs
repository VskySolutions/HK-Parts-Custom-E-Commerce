using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.PriceRules;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>
/// Storefront/cart/checkout price resolution:
/// <list type="number">
/// <item><b>Base</b> — the Customer Group price: a fixed group price (variant row over product row), a group
/// percentage off the list price, or the list price. Retail (no group / guest / inactive group) = list price.</item>
/// <item><b>Sale</b> — <see cref="Product.SalePrice"/> while now is inside its UTC window (product-level lines;
/// a variant carries its own price, so the product's sale price does not apply to it).</item>
/// <item><b>Rule</b> — the lowest pre-calculated <see cref="PriceRuleProductPrice"/> among the rules in force
/// today, walked by priority up to the first "stop further rules" (rules never stack).</item>
/// </list>
/// The lowest of the three wins; nothing ever raises the price. Rules are read from the flat table through
/// its covering index and never evaluated live.
/// </summary>
public class PriceResolver : IPriceResolver
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public PriceResolver(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PriceResolution> GetPriceAsync(
        Guid productId, Guid? variantId, Guid? customerGroupId, Guid? storeId,
        DateTime? nowUtc = null, CancellationToken cancellationToken = default)
    {
        decimal? listPrice;
        if (variantId is Guid vid)
        {
            listPrice = await _db.ProductVariants.AsNoTracking()
                .Where(v => v.Id == vid && v.ProductId == productId)
                .Select(v => v.Price ?? v.Product!.Price)
                .FirstOrDefaultAsync(cancellationToken);
        }
        else
        {
            listPrice = await _db.Products.AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => p.Price)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var request = new GroupPriceRequest(productId, variantId, listPrice ?? 0m);
        var result = await GetPricesAsync(new[] { request }, customerGroupId, storeId, nowUtc, cancellationToken);
        return result[new GroupPriceKey(productId, variantId)];
    }

    public async Task<IReadOnlyDictionary<GroupPriceKey, PriceResolution>> GetPricesAsync(
        IReadOnlyCollection<GroupPriceRequest> items, Guid? customerGroupId, Guid? storeId,
        DateTime? nowUtc = null, CancellationToken cancellationToken = default)
    {
        var now = nowUtc ?? _clock.UtcNow;
        var result = new Dictionary<GroupPriceKey, PriceResolution>(items.Count);
        if (items.Count == 0)
            return result;

        // 1. Customer Group base price.
        var (bases, effectiveGroupId) = await ResolveGroupBaseAsync(items, customerGroupId, cancellationToken);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        // 2. Active sale prices (product-level lines only).
        var saleProductIds = items.Where(i => i.VariantId is null).Select(i => i.ProductId).Distinct().ToList();
        var sales = saleProductIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await _db.Products.AsNoTracking()
                .Where(p => saleProductIds.Contains(p.Id) && p.SalePrice != null
                            && (p.SaleStartDateUtc == null || p.SaleStartDateUtc <= now)
                            && (p.SaleEndDateUtc == null || p.SaleEndDateUtc >= now))
                .ToDictionaryAsync(p => p.Id, p => p.SalePrice!.Value, cancellationToken);

        // 3. Pre-calculated rule prices — served by IX_PriceRuleProductPrices_Lookup. Rows are narrowed to
        // ±1 day in SQL (store zones are at most ±14h from UTC) and to exactly "today" per store below.
        var utcToday = DateOnly.FromDateTime(now);
        var lowDate = utcToday.AddDays(-1);
        var highDate = utcToday.AddDays(1);
        var rows = await _db.PriceRuleProductPrices.AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId)
                        && x.CustomerGroupId == effectiveGroupId
                        && (storeId == null || x.StoreId == null || x.StoreId == storeId)
                        && (x.StartDate == null || x.StartDate <= highDate)
                        && (x.EndDate == null || x.EndDate >= lowDate))
            .Select(x => new
            {
                x.ProductId, x.ProductVariantId, x.StoreId, x.PriceRuleId, x.RulePrice,
                x.StartDate, x.EndDate, x.Priority, x.StopFurtherRules,
            })
            .ToListAsync(cancellationToken);

        var clock = rows.Count == 0 ? null : await PriceRuleClock.LoadAsync(_db, cancellationToken);
        var rowStore = rows.GroupBy(r => r.PriceRuleId).ToDictionary(g => g.Key, g => g.First().StoreId);
        var byKey = rows.ToLookup(
            r => new GroupPriceKey(r.ProductId, r.ProductVariantId),
            r => new RulePriceCandidate(r.PriceRuleId, r.RulePrice, r.StartDate, r.EndDate, r.Priority, r.StopFurtherRules));

        foreach (var item in items)
        {
            var key = new GroupPriceKey(item.ProductId, item.VariantId);
            if (result.ContainsKey(key))
                continue;

            var basePrice = bases[key];
            decimal? sale = item.VariantId is null && sales.TryGetValue(item.ProductId, out var s) ? s : null;

            var pick = clock is null
                ? null
                : PriceRuleMath.PickRulePrice(byKey[key], c => clock.TodayFor(rowStore[c.PriceRuleId], now));

            var (final, onSale, ruleApplied) = PriceRuleMath.Combine(basePrice, sale, pick?.Price);
            result[key] = new PriceResolution(basePrice, final, onSale, ruleApplied, ruleApplied ? pick!.Value.RuleId : null);
        }

        return result;
    }

    /// <summary>
    /// The group base price per key, plus the group id the flat table is keyed by (the Retail sentinel for
    /// guests, customers without a group, and inactive/deleted groups).
    /// </summary>
    private async Task<(Dictionary<GroupPriceKey, decimal> Bases, Guid GroupId)> ResolveGroupBaseAsync(
        IReadOnlyCollection<GroupPriceRequest> items, Guid? groupId, CancellationToken ct)
    {
        var bases = new Dictionary<GroupPriceKey, decimal>(items.Count);
        foreach (var item in items)
            bases[new GroupPriceKey(item.ProductId, item.VariantId)] = item.BasePrice;

        if (groupId is not Guid gid || gid == PriceRuleCustomerGroup.RetailGroupId)
            return (bases, PriceRuleCustomerGroup.RetailGroupId);

        // The soft-delete query filter excludes deleted groups.
        var group = await _db.CustomerGroups.AsNoTracking()
            .Where(g => g.Id == gid && g.IsActive)
            .Select(g => new { g.PricingRuleType, g.DiscountPercent })
            .FirstOrDefaultAsync(ct);

        if (group is null)
            return (bases, PriceRuleCustomerGroup.RetailGroupId);

        if (group.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount)
        {
            var percent = Math.Clamp(group.DiscountPercent ?? 0m, 0m, 100m);
            if (percent > 0m)
            {
                foreach (var item in items)
                    bases[new GroupPriceKey(item.ProductId, item.VariantId)] =
                        Math.Round(item.BasePrice * (1m - percent / 100m), 2, MidpointRounding.AwayFromZero);
            }

            return (bases, gid);
        }

        if (group.PricingRuleType != CustomerGroupPricingRuleType.FixedGroupPrice)
            return (bases, gid);

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var rows = await _db.CustomerGroupPrices.AsNoTracking()
            .Where(p => p.CustomerGroupId == gid && productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductVariantId, p.Price })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return (bases, gid);

        // The unique index (CustomerGroupId, ProductId, ProductVariantId) guarantees one row per key; group
        // defensively so a stray duplicate picks the cheapest rather than throwing.
        var variantPrices = rows.Where(r => r.ProductVariantId != null)
            .GroupBy(r => (r.ProductId, VariantId: r.ProductVariantId!.Value))
            .ToDictionary(g => g.Key, g => g.Min(x => x.Price));
        var productPrices = rows.Where(r => r.ProductVariantId == null)
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Price));

        foreach (var item in items)
        {
            // A fixed group price is authoritative (the admin set it deliberately), even above the list price.
            var key = new GroupPriceKey(item.ProductId, item.VariantId);
            if (item.VariantId is Guid vid && variantPrices.TryGetValue((item.ProductId, vid), out var variantPrice))
                bases[key] = variantPrice;
            else if (productPrices.TryGetValue(item.ProductId, out var productPrice))
                bases[key] = productPrice;
        }

        return (bases, gid);
    }
}
