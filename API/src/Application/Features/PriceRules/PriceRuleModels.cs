using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.PriceRules;

/// <summary>Where a rule is in its life, as of today in the rule's store time zone.</summary>
public enum PriceRuleEffectiveState
{
    Inactive = 0,
    Scheduled = 1,
    Running = 2,
    Expired = 3,
}

public class PriceRuleListItemDto
{
    public Guid Id { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public PriceRuleEffectiveState EffectiveState { get; set; }
    public PriceRuleDiscountType DiscountType { get; set; }
    public decimal DiscountAmount { get; set; }
    public int Priority { get; set; }
    public bool StopFurtherRules { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int ProductsAffected { get; set; }
    public DateTime? LastAppliedAtUtc { get; set; }
}

public class PriceRuleDto
{
    public Guid Id { get; set; }
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public PriceRuleEffectiveState EffectiveState { get; set; }
    public int Priority { get; set; }
    public bool StopFurtherRules { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public PriceRuleDiscountType DiscountType { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime? LastAppliedAtUtc { get; set; }
    public List<Guid> CustomerGroupIds { get; set; } = new();
    public List<Guid> CategoryIds { get; set; } = new();
    /// <summary>Base64 rowversion; send it back on update for optimistic concurrency.</summary>
    public string RowVersion { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static PriceRuleDto From(PriceRule r, DateOnly today) => new()
    {
        Id = r.Id,
        StoreId = r.StoreId,
        Name = r.Name,
        Description = r.Description,
        IsActive = r.IsActive,
        EffectiveState = PriceRuleStates.Of(r.IsActive, r.StartDate, r.EndDate, today),
        Priority = r.Priority,
        StopFurtherRules = r.StopFurtherRules,
        StartDate = r.StartDate,
        EndDate = r.EndDate,
        DiscountType = r.DiscountType,
        DiscountAmount = r.DiscountAmount,
        LastAppliedAtUtc = r.LastAppliedAtUtc,
        CustomerGroupIds = r.CustomerGroups.Select(g => g.CustomerGroupId).OrderBy(g => g).ToList(),
        CategoryIds = r.Categories.Select(c => c.CategoryId).OrderBy(c => c).ToList(),
        RowVersion = Convert.ToBase64String(r.RowVersion ?? Array.Empty<byte>()),
        CreatedOnUtc = r.CreatedOnUtc,
        UpdatedOnUtc = r.UpdatedOnUtc,
    };

    /// <summary>The audit snapshot (no rowversion/timestamps — just what an admin changed).</summary>
    public object ToAuditSnapshot() => new
    {
        StoreId, Name, Description, IsActive, Priority, StopFurtherRules, StartDate, EndDate,
        DiscountType = DiscountType.ToString(), DiscountAmount, CustomerGroupIds, CategoryIds,
    };
}

/// <summary>A pre-calculated price row of a saved rule.</summary>
public class PriceRuleProductPriceDto
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

/// <summary>A customer group as the rule form lists it (includes the "Retail" sentinel).</summary>
public class PriceRuleGroupOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public static class PriceRuleStates
{
    public const string RetailGroupName = "Retail (guests & no group)";

    public static PriceRuleEffectiveState Of(bool isActive, DateOnly? start, DateOnly? end, DateOnly today)
    {
        if (!isActive) return PriceRuleEffectiveState.Inactive;
        if (end is DateOnly e && e < today) return PriceRuleEffectiveState.Expired;
        if (start is DateOnly s && s > today) return PriceRuleEffectiveState.Scheduled;
        return PriceRuleEffectiveState.Running;
    }
}
