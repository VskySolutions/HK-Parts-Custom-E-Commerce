using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Per-category dynamic storefront configuration: banner image, pinned products, promotional
/// description, and a "You May Also Like" collection (WO-99). One row per category. CMS-owned: table
/// name is <c>CMSCategoryPageConfigs</c>.</summary>
public class CMSCategoryPageConfig : AuditableEntity
{
    public Guid CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid? BannerMediaId { get; set; }
    public Media? BannerMedia { get; set; }

    public string? PromotionalDescription { get; set; }

    public Guid? YmalCollectionId { get; set; }
    public CMSProductCollection? YmalCollection { get; set; }

    public ICollection<CMSCategoryPinnedProduct> PinnedProducts { get; set; } = new List<CMSCategoryPinnedProduct>();
}
