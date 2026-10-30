using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>A merchant-defined free-text product field.</summary>
public class ProductCustomFieldDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ProductCustomFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }

    public static ProductCustomFieldDto From(ProductCustomField f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        Code = f.Code,
        FieldType = f.FieldType,
        IsRequired = f.IsRequired,
        IsActive = f.IsActive,
        DisplayOrder = f.DisplayOrder,
    };
}
