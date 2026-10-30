using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A single ordered product assignment within a <see cref="CMSProductCollection"/> (WO-97).
/// CMS-owned: table name is <c>CMSProductCollectionItems</c>.</summary>
public class CMSProductCollectionItem : BaseEntity
{
    public Guid CollectionId { get; set; }
    public CMSProductCollection? Collection { get; set; }

    public Guid ProductId { get; set; }
    public Product? Product { get; set; }

    public int DisplayOrder { get; set; }
}
