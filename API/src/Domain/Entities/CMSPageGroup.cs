using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>An organising group for CMS pages (e.g. "Customer Service", "About"), used to build
/// storefront footer/nav link columns (WO-54). CMS-owned: table name is <c>CMSPageGroups</c>.</summary>
public class CMSPageGroup : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public int DisplayOrder { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<CMSPage> Pages { get; set; } = new List<CMSPage>();
}
