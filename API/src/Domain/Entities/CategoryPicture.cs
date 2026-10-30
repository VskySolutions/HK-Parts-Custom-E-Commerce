using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Assigns a centralized <see cref="Media"/> asset to a category as one of its pictures, mirroring
/// <see cref="ProductPicture"/> (WO-123 pattern, REQ-CAT-012). The image bytes, SEO file name and alt
/// text live on the Media record; this row only orders the asset on the category.
/// </summary>
public class CategoryPicture : BaseEntity
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid MediaId { get; set; }
    public Media? Media { get; set; }

    public int DisplayOrder { get; set; }
}
