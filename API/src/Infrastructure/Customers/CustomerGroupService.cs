using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Customers;

/// <summary>
/// Customer-group membership lookup plus the price funnel every storefront/cart/checkout path calls
/// (REQ-CUS-003, AC-CUS-003.5).
/// <para>
/// Price resolution is delegated to <see cref="IPriceResolver"/>, which layers the group price (None / fixed
/// group price / percentage), an active sale price, and pre-calculated price rules — so a shopper sees the same
/// price while browsing, in the cart and at checkout. Inactive and soft-deleted groups price as retail.
/// </para>
/// </summary>
public class CustomerGroupService : ICustomerGroupService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IPriceResolver _prices;

    public CustomerGroupService(IApplicationDbContext db, ICurrentUserService current, IPriceResolver prices)
    {
        _db = db;
        _current = current;
        _prices = prices;
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
        // No store context on the storefront today: rules of every store apply (see IPriceResolver).
        var resolved = await _prices.GetPricesAsync(items, groupId, storeId: null, cancellationToken: cancellationToken);
        return resolved.ToDictionary(kv => kv.Key, kv => kv.Value.FinalPrice);
    }
}
