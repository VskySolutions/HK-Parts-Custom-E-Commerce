using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

/// <summary>A reusable specification attribute (filterable facet, e.g. "Material") and its options.</summary>
public class SpecificationAttributeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsFilterable { get; set; }
    public int DisplayOrder { get; set; }
    public List<SpecificationAttributeOptionDto> Options { get; set; } = new();

    public static SpecificationAttributeDto From(SpecificationAttribute a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        IsFilterable = a.IsFilterable,
        DisplayOrder = a.DisplayOrder,
        Options = a.Options
            .OrderBy(o => o.DisplayOrder)
            .Select(SpecificationAttributeOptionDto.From)
            .ToList(),
    };
}

/// <summary>A single option/value of a <see cref="SpecificationAttributeDto"/>.</summary>
public class SpecificationAttributeOptionDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public static SpecificationAttributeOptionDto From(SpecificationAttributeOption o) => new()
    {
        Id = o.Id,
        Value = o.Value,
        DisplayOrder = o.DisplayOrder,
    };
}
