using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Customers;

/// <summary>
/// Group-based pricing resolution (REQ-CUS-003).
/// <para>
/// Resolution is driven by the group's single <see cref="CustomerGroupPricingRuleType"/>, so the price a
/// member pays is predictable from the group definition alone:
/// <list type="bullet">
/// <item>None — base price.</item>
/// <item>PercentageDiscount — base price less DiscountPercent (AC-CUS-003.3).</item>
/// <item>FixedGroupPrice — the variant-specific group price, else the product-level one, else base
/// (AC-CUS-003.4). A fixed price is authoritative: the admin set it deliberately, so it applies even if it
/// is above the base price.</item>
/// </list>
/// Inactive and soft-deleted groups never price (the query filter covers the latter).
/// </para>
/// </summary>
public class CustomerGroupService : ICustomerGroupService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public CustomerGroupService(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<Guid?> GetCurrentGroupIdAsync(CancellationToken cancellationToken = default)
    {
        if (_current.UserId is not Guid userId)
            return null;

        return await _db.Customers
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .Select(c => c.CustomerGroupId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetGroupIdForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _db.Customers
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => c.CustomerGroupId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<decimal> ResolvePriceAsync(
        Guid productId, Guid? variantId, decimal basePrice, Guid? groupId, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolvePricesAsync(
            new[] { new GroupPriceRequest(productId, variantId, basePrice) }, groupId, cancellationToken);

        return resolved.TryGetValue(new GroupPriceKey(productId, variantId), out var price) ? price : basePrice;
    }

    public async Task<IReadOnlyDictionary<GroupPriceKey, decimal>> ResolvePricesAsync(
        IReadOnlyCollection<GroupPriceRequest> items, Guid? groupId, CancellationToken cancellationToken = default)
    {
        // Every key is present in the result, defaulted to base, so callers can index without a fallback.
        var result = new Dictionary<GroupPriceKey, decimal>(items.Count);
        foreach (var item in items)
            result[new GroupPriceKey(item.ProductId, item.VariantId)] = item.BasePrice;

        if (items.Count == 0 || groupId is not Guid gid)
            return result;

        var group = await _db.CustomerGroups
            .AsNoTracking()
            .Where(g => g.Id == gid && g.IsActive)
            .Select(g => new { g.PricingRuleType, g.DiscountPercent })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
            return result;

        if (group.PricingRuleType == CustomerGroupPricingRuleType.PercentageDiscount)
        {
            var percent = Math.Clamp(group.DiscountPercent ?? 0m, 0m, 100m);
            if (percent <= 0m)
                return result;

            foreach (var item in items)
            {
                result[new GroupPriceKey(item.ProductId, item.VariantId)] =
                    Math.Round(item.BasePrice * (1m - percent / 100m), 2, MidpointRounding.AwayFromZero);
            }

            return result;
        }

        if (group.PricingRuleType != CustomerGroupPricingRuleType.FixedGroupPrice)
            return result;

        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var rows = await _db.CustomerGroupPrices
            .AsNoTracking()
            .Where(p => p.CustomerGroupId == gid && productIds.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.ProductVariantId, p.Price })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return result;

        // Unique index (CustomerGroupId, ProductId, ProductVariantId) guarantees one row per key, but group
        // defensively so a stray duplicate picks the cheapest rather than throwing.
        var variantPrices = rows
            .Where(r => r.ProductVariantId != null)
            .GroupBy(r => (r.ProductId, VariantId: r.ProductVariantId!.Value))
            .ToDictionary(g => g.Key, g => g.Min(x => x.Price));

        var productPrices = rows
            .Where(r => r.ProductVariantId == null)
            .GroupBy(r => r.ProductId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Price));

        foreach (var item in items)
        {
            // Variant-specific price wins over the product-level price for that variant (AC-CUS-003.4).
            if (item.VariantId is Guid vid && variantPrices.TryGetValue((item.ProductId, vid), out var variantPrice))
                result[new GroupPriceKey(item.ProductId, item.VariantId)] = variantPrice;
            else if (productPrices.TryGetValue(item.ProductId, out var productPrice))
                result[new GroupPriceKey(item.ProductId, item.VariantId)] = productPrice;
        }

        return result;
    }
}
