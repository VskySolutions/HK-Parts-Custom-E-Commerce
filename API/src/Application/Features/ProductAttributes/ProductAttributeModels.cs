using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

/// <summary>A reusable product attribute (e.g. "Colour", "Size") and its selectable values.</summary>
public class ProductAttributeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Presentation of the attribute (Dropdown/Button/Swatch/CustomInput); transported as the enum name.</summary>
    public string DisplayType { get; set; } = nameof(ProductAttributeDisplayType.Dropdown);
    public int DisplayOrder { get; set; }

    /// <summary>CustomInput only: Text or Number; transported as the enum name.</summary>
    public string InputType { get; set; } = nameof(ProductAttributeInputType.Text);

    /// <summary>CustomInput only: maximum length the buyer may type; null = unlimited.</summary>
    public int? MaxLength { get; set; }

    /// <summary>CustomInput only: whether the buyer has to fill the field in before adding to the cart.</summary>
    public bool IsRequired { get; set; }

    /// <summary>Number of products this attribute is assigned to; drives the "In use" column + delete protection (list only).</summary>
    public int InUseCount { get; set; }
    public List<ProductAttributeValueDto> Values { get; set; } = new();

    public static ProductAttributeDto From(ProductAttribute a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Description = a.Description,
        DisplayType = a.DisplayType.ToString(),
        DisplayOrder = a.DisplayOrder,
        InputType = a.InputType.ToString(),
        MaxLength = a.MaxLength,
        IsRequired = a.IsRequired,
        Values = a.Values
            .OrderBy(v => v.DisplayOrder)
            .Select(ProductAttributeValueDto.From)
            .ToList(),
    };
}

/// <summary>A single selectable value of a <see cref="ProductAttributeDto"/> (e.g. "Red", "Large").</summary>
public class ProductAttributeValueDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;

    /// <summary>Optional hex colour, populated when the parent attribute's display type is Swatch.</summary>
    public string? ColorHex { get; set; }
    public int DisplayOrder { get; set; }

    public static ProductAttributeValueDto From(ProductAttributeValue v) => new()
    {
        Id = v.Id,
        Value = v.Value,
        ColorHex = v.ColorHex,
        DisplayOrder = v.DisplayOrder,
    };
}
