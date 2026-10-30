using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A merchant-defined free-text product field (e.g. "Caliber", "Warranty notes"). Option-valued,
/// filterable properties belong in <see cref="SpecificationAttribute"/>; this covers the free-text ones.
/// </summary>
public class ProductCustomField : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    /// <summary>Stable key used by import/export and templates.</summary>
    public string Code { get; set; } = string.Empty;
    public ProductCustomFieldType FieldType { get; set; } = ProductCustomFieldType.Text;
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ProductCustomFieldValue> Values { get; set; } = new List<ProductCustomFieldValue>();
}

/// <summary>A product's value for one <see cref="ProductCustomField"/>.</summary>
public class ProductCustomFieldValue : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProductCustomFieldId { get; set; }
    public ProductCustomField? ProductCustomField { get; set; }
    public string? Value { get; set; }
}
