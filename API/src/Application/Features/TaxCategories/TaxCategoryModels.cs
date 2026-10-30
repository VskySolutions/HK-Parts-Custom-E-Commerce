using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.TaxCategories;

/// <summary>A tax classification products are assigned to (AC-CAT-001.6).</summary>
public class TaxCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? DefaultRatePercent { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public static TaxCategoryDto From(TaxCategory t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Description = t.Description,
        DefaultRatePercent = t.DefaultRatePercent,
        DisplayOrder = t.DisplayOrder,
        IsActive = t.IsActive,
    };
}
