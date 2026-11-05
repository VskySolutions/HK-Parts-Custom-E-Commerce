using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An automatic catalog discount (no coupon): lowers the price of every product directly assigned to the
/// selected <see cref="Categories"/> for the selected <see cref="CustomerGroups"/>, optionally inside an
/// inclusive <see cref="StartDate"/>..<see cref="EndDate"/> window.
/// <para>
/// Rules are never evaluated live. A background rebuild pre-calculates every (product/variant × group) price
/// into <see cref="PriceRuleProductPrice"/>; the storefront, cart and checkout read that flat table.
/// </para>
/// </summary>
public class PriceRule : AuditableEntity
{
    /// <summary>The store the rule belongs to; null = every store (the storefront has no store context today).</summary>
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Lower runs first.</summary>
    public int Priority { get; set; }

    /// <summary>When true, lower-priority rules are not considered after this one.</summary>
    public bool StopFurtherRules { get; set; }

    /// <summary>Inclusive start date in the store's time zone; null = no start bound.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Inclusive end date in the store's time zone; null = no end bound.</summary>
    public DateOnly? EndDate { get; set; }

    public PriceRuleDiscountType DiscountType { get; set; } = PriceRuleDiscountType.Percent;
    public decimal DiscountAmount { get; set; }

    /// <summary>When the flat price table was last rebuilt for this rule (UTC).</summary>
    public DateTime? LastAppliedAtUtc { get; set; }

    /// <summary>Optimistic-concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<PriceRuleCustomerGroup> CustomerGroups { get; set; } = new List<PriceRuleCustomerGroup>();
    public ICollection<PriceRuleCategory> Categories { get; set; } = new List<PriceRuleCategory>();
}

/// <summary>
/// A customer group a <see cref="PriceRule"/> targets. <see cref="PriceRuleCustomerGroup.RetailGroupId"/>
/// (<see cref="Guid.Empty"/>) stands for the default/retail audience — guests and customers with no group.
/// The group id is a plain id (no FK) so the retail sentinel can be stored.
/// </summary>
public class PriceRuleCustomerGroup
{
    /// <summary>The default/retail "group": guests and customers without a Customer Group.</summary>
    public static readonly Guid RetailGroupId = Guid.Empty;

    public Guid PriceRuleId { get; set; }
    public PriceRule? PriceRule { get; set; }
    public Guid CustomerGroupId { get; set; }
}

/// <summary>A category a <see cref="PriceRule"/> targets (direct assignment only — no descendants).</summary>
public class PriceRuleCategory
{
    public Guid PriceRuleId { get; set; }
    public PriceRule? PriceRule { get; set; }
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }
}

/// <summary>
/// One pre-calculated rule price: rule × product (or variant) × customer group. Written only by the set-based
/// rebuild; the dates, priority and stop flag are copied from the rule so price resolution needs no join.
/// </summary>
public class PriceRuleProductPrice
{
    public long Id { get; set; }
    public Guid PriceRuleId { get; set; }
    public PriceRule? PriceRule { get; set; }
    public Guid? StoreId { get; set; }

    public Guid ProductId { get; set; }
    /// <summary>Set for a variant of a <see cref="ProductType.WithVariants"/> product; null = the product itself.</summary>
    public Guid? ProductVariantId { get; set; }
    public Guid CustomerGroupId { get; set; }

    public decimal BasePrice { get; set; }
    public decimal RulePrice { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int Priority { get; set; }
    public bool StopFurtherRules { get; set; }
}
