using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A reusable specification attribute in the global library (e.g. "Screen Size", "Material").
/// When <see cref="IsFilterable"/> is set it participates in storefront faceted navigation
/// (REQ-CAT-003, AC-CAT-003.2/3.3).
/// </summary>
public class SpecificationAttribute : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Whether this specification appears in storefront faceted navigation (AC-CAT-003.3).</summary>
    public bool IsFilterable { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<SpecificationAttributeOption> Options { get; set; } = new List<SpecificationAttributeOption>();
}

/// <summary>A possible option/value of a <see cref="SpecificationAttribute"/>.</summary>
public class SpecificationAttributeOption : AuditableEntity
{
    public Guid SpecificationAttributeId { get; set; }
    public SpecificationAttribute? SpecificationAttribute { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<ProductSpecificationValue> ProductValues { get; set; } = new List<ProductSpecificationValue>();
}
