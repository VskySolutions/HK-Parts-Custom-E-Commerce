using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>An ordered pinned product on a category page, injected ahead of the regular product grid
/// regardless of sort (WO-99). CMS-owned: table name is <c>CMSCategoryPinnedProducts</c>.</summary>
public class CMSCategoryPinnedProduct : BaseEntity
{
    public Guid CategoryPageConfigId { get; set; }
    public CMSCategoryPageConfig? CategoryPageConfig { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public int DisplayOrder { get; set; }
}
