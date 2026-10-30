using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An admin-defined pricing group (REQ-CUS-003) — e.g. Wholesale, VIP, Retail. Carries group-pricing
/// rules ONLY: it never controls catalog visibility (AC-CUS-003.6).
/// <para>
/// Distinct from the RBAC <see cref="Role"/>: every registered shopper carries the
/// <see cref="RoleType.Customer"/> system role, which grants storefront access. A Customer Group is an
/// optional pricing overlay on top of that, assigned via <see cref="Customer.CustomerGroupId"/> — at
/// most one per customer (AC-CUS-003.2). No group = base pricing.
/// </para>
/// </summary>
public class CustomerGroup : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public CustomerGroupPricingRuleType PricingRuleType { get; set; } = CustomerGroupPricingRuleType.None;

    /// <summary>Percentage off the base price when <see cref="PricingRuleType"/> is PercentageDiscount (0–100).</summary>
    public decimal? DiscountPercent { get; set; }

    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<CustomerGroupPrice> GroupPrices { get; set; } = new List<CustomerGroupPrice>();
}

/// <summary>
/// An explicit price for a product/variant for members of a group (AC-CUS-003.4). A variant-specific row
/// (<see cref="ProductVariantId"/> set) overrides the product-level row for that variant.
/// </summary>
public class CustomerGroupPrice : BaseEntity
{
    public Guid CustomerGroupId { get; set; }
    public CustomerGroup? CustomerGroup { get; set; }

    /// <summary>Product/variant are plain ids (no FK) to avoid SQL Server multiple-cascade-path errors.</summary>
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Price { get; set; }
}
