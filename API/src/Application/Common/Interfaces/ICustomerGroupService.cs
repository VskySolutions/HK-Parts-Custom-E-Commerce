namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Identifies a product, or a specific variant of one, for group-price resolution.</summary>
public readonly record struct GroupPriceKey(Guid ProductId, Guid? VariantId);

/// <summary>A single "what does this group pay for this item?" question.</summary>
public readonly record struct GroupPriceRequest(Guid ProductId, Guid? VariantId, decimal BasePrice);

/// <summary>
/// Resolves Customer Group pricing (REQ-CUS-003). Every storefront/cart/checkout price path calls this so a
/// group member sees the same price while browsing, in the cart, and at checkout (AC-CUS-003.5).
/// <para>
/// A customer belongs to at most one group (<see cref="Domain.Entities.Customer.CustomerGroupId"/>); no group,
/// an inactive/deleted group, or a guest all resolve to the base price. Groups never affect visibility
/// (AC-CUS-003.6).
/// </para>
/// </summary>
public interface ICustomerGroupService
{
    /// <summary>The calling user's pricing group, or null for guests / customers with no group.</summary>
    Task<Guid?> GetCurrentGroupIdAsync(CancellationToken cancellationToken = default);

    /// <summary>A specific customer's pricing group, or null.</summary>
    Task<Guid?> GetGroupIdForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The effective price for one product/variant. Honours the group's rule type: a percentage discount off
    /// the base price (AC-CUS-003.3), or an explicit fixed group price where a variant-specific row overrides
    /// the product-level one (AC-CUS-003.4). Returns <paramref name="basePrice"/> when no rule applies.
    /// </summary>
    Task<decimal> ResolvePriceAsync(
        Guid productId, Guid? variantId, decimal basePrice, Guid? groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk form of <see cref="ResolvePriceAsync"/> — resolves a whole listing page in a fixed number of
    /// queries instead of one pair per line. Every requested key is present in the result.
    /// </summary>
    Task<IReadOnlyDictionary<GroupPriceKey, decimal>> ResolvePricesAsync(
        IReadOnlyCollection<GroupPriceRequest> items, Guid? groupId, CancellationToken cancellationToken = default);
}
