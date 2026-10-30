using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>An admin-curated, ordered list of products used as a data source for home page Product Rows
/// and category "You May Also Like" rows (WO-97). CMS-owned: table name is
/// <c>CMSProductCollections</c>.</summary>
public class CMSProductCollection : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<CMSProductCollectionItem> Items { get; set; } = new List<CMSProductCollectionItem>();
}
