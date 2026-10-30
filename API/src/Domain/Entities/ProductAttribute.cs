using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A reusable product attribute in the global attribute library (e.g. "Colour", "Size") used to
/// generate product variants (REQ-CAT-003, AC-CAT-003.1).
/// </summary>
public class ProductAttribute : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>How the attribute's values are presented for variant selection (WO-15).</summary>
    public ProductAttributeDisplayType DisplayType { get; set; } = ProductAttributeDisplayType.Dropdown;
    public int DisplayOrder { get; set; }

    /// <summary>CustomInput only: whether the buyer types free text or a number.</summary>
    public ProductAttributeInputType InputType { get; set; } = ProductAttributeInputType.Text;

    /// <summary>CustomInput only: maximum characters (Text) or digits (Number) accepted; null = unlimited.</summary>
    public int? MaxLength { get; set; }

    /// <summary>CustomInput only: the buyer must fill the field in before the product can be added to the cart.</summary>
    public bool IsRequired { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<ProductAttributeValue> Values { get; set; } = new List<ProductAttributeValue>();
    public ICollection<ProductAttributeMapping> ProductMappings { get; set; } = new List<ProductAttributeMapping>();
}

/// <summary>A possible value of a <see cref="ProductAttribute"/> (e.g. "Red", "Large").</summary>
public class ProductAttributeValue : AuditableEntity
{
    public Guid ProductAttributeId { get; set; }
    public ProductAttribute? ProductAttribute { get; set; }
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional hex colour (e.g. "#FF0000") shown when the attribute's display type is Swatch (WO-15).</summary>
    public string? ColorHex { get; set; }
    public int DisplayOrder { get; set; }
}
