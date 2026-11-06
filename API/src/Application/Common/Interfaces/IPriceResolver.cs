namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// The price a shopper pays for one product/variant before option add-ons and cart-level discounts.
/// </summary>
/// <param name="BasePrice">The customer group's base price (group price/percentage applied to the list price).</param>
/// <param name="FinalPrice">The lowest of base, an active sale price and the winning price-rule price.</param>
/// <param name="OnSale">An active sale price is at or below base.</param>
/// <param name="RuleApplied">A price rule beat the base/sale price.</param>
/// <param name="PriceRuleId">The rule that set <see cref="FinalPrice"/>, when <see cref="RuleApplied"/>.</param>
public sealed record PriceResolution(
    decimal BasePrice, decimal FinalPrice, bool OnSale, bool RuleApplied, Guid? PriceRuleId);

/// <summary>
/// Resolves storefront/cart/checkout prices: Customer Group base → active sale price → pre-calculated
/// price rules (read from the flat <c>PriceRuleProductPrices</c> table, never evaluated live). The lowest
/// wins; a rule never raises a price and rules never stack.
/// </summary>
public interface IPriceResolver
{
    /// <summary>One product (or variant). Loads the list price itself.</summary>
    /// <param name="customerGroupId">Null = retail (guests / customers without a group).</param>
    /// <param name="storeId">Null = no store context: rules of every store apply.</param>
    /// <param name="nowUtc">Defaults to the clock; exposed so tests can price "tomorrow".</param>
    Task<PriceResolution> GetPriceAsync(
        Guid productId, Guid? variantId, Guid? customerGroupId, Guid? storeId,
        DateTime? nowUtc = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch form for listing pages, carts and checkout — a fixed number of queries regardless of size.
    /// <see cref="GroupPriceRequest.BasePrice"/> is the list price (product or variant price). Every
    /// requested key is present in the result.
    /// </summary>
    Task<IReadOnlyDictionary<GroupPriceKey, PriceResolution>> GetPricesAsync(
        IReadOnlyCollection<GroupPriceRequest> items, Guid? customerGroupId, Guid? storeId,
        DateTime? nowUtc = null, CancellationToken cancellationToken = default);
}
