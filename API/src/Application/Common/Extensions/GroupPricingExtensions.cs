using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Common.Extensions;

/// <summary>
/// Overlays Customer Group pricing onto already-projected read DTOs (AC-CUS-003.5).
/// <para>
/// The storefront/cart DTOs are built by static <c>From(entity)</c> mappers that only see the base price, so
/// group pricing is applied as a second pass over the projected page. Doing it here — rather than inside each
/// mapper — keeps it to a fixed number of queries per request and leaves the persisted base price untouched,
/// which matters because checkout re-resolves the group price from that same base (applying a percentage rule
/// to an already-discounted price would discount twice).
/// </para>
/// </summary>
public static class GroupPricingExtensions
{
    /// <summary>
    /// Replaces each item's price with its group price. A no-op when <paramref name="groupId"/> is null
    /// (guest / no group), so the anonymous path costs nothing.
    /// </summary>
    /// <param name="read">Returns the item's price question, or null to leave the item alone (e.g. no price).</param>
    /// <param name="write">Applies the resolved price back onto the item.</param>
    public static async Task ApplyGroupPricingAsync<T>(
        this ICustomerGroupService groups,
        IReadOnlyCollection<T> items,
        Guid? groupId,
        Func<T, GroupPriceRequest?> read,
        Action<T, decimal> write,
        CancellationToken cancellationToken = default)
    {
        if (groupId is null || items.Count == 0)
            return;

        var requests = new List<GroupPriceRequest>(items.Count);
        foreach (var item in items)
        {
            if (read(item) is GroupPriceRequest request)
                requests.Add(request);
        }

        if (requests.Count == 0)
            return;

        var prices = await groups.ResolvePricesAsync(requests, groupId, cancellationToken);

        foreach (var item in items)
        {
            if (read(item) is not GroupPriceRequest request)
                continue;
            if (prices.TryGetValue(new GroupPriceKey(request.ProductId, request.VariantId), out var price)
                && price != request.BasePrice)
            {
                write(item, price);
            }
        }
    }
}
