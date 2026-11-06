using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>An unsaved (or saved) rule definition, as the engine needs it to compute prices.</summary>
public sealed record PriceRuleDraft(
    Guid? StoreId,
    bool IsActive,
    DateOnly? EndDate,
    PriceRuleDiscountType DiscountType,
    decimal DiscountAmount,
    IReadOnlyCollection<Guid> CustomerGroupIds,
    IReadOnlyCollection<Guid> CategoryIds);

/// <summary>One sample row of a preview.</summary>
public sealed class PriceRulePreviewRow
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CustomerGroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal RulePrice { get; set; }
}

public sealed class PriceRulePreviewResult
{
    /// <summary>Distinct products (variants roll up under their product) the rule would price.</summary>
    public int ProductCount { get; set; }
    /// <summary>Total flat rows (product/variant × group) the rule would write.</summary>
    public int PriceRowCount { get; set; }
    public List<PriceRulePreviewRow> Sample { get; set; } = new();
}

/// <summary>
/// Rebuilds the flat <c>PriceRuleProductPrices</c> table with set-based SQL — one transaction per rule,
/// serialized per rule by an application lock so two rebuilds of the same rule never interleave.
/// </summary>
public interface IPriceRuleEngine
{
    /// <summary>Rebuild one rule. A missing rule is a no-op (it may have been deleted since it was queued).</summary>
    Task RebuildAsync(Guid ruleId, CancellationToken cancellationToken = default);

    /// <summary>Rebuild every rule, optionally only those of one store. Returns how many were rebuilt.</summary>
    Task<int> RebuildAllAsync(Guid? storeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuild every rule that targets (via a category) or already prices any of the given products — the
    /// trigger for price / group-price / category / store-assignment changes and product deletion.
    /// </summary>
    Task<int> RebuildForProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default);

    /// <summary>What a rule would write, without writing it: counts plus the first 50 rows.</summary>
    Task<PriceRulePreviewResult> PreviewAsync(PriceRuleDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fire-and-forget rebuild requests, coalesced and run by a single background consumer (so a burst — e.g. a
/// bulk import touching thousands of products — becomes one rebuild per affected rule).
/// </summary>
public interface IPriceRuleRebuildQueue
{
    void EnqueueRule(Guid ruleId);
    void EnqueueAll();
    void EnqueueForProducts(IEnumerable<Guid> productIds);
}
