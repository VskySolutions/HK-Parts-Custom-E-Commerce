using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A configurable, ordered section on the storefront home page (WO-96). The type-specific
/// settings (max items, product-row source, banner/collection/rule references, custom HTML) are stored
/// as JSON in <see cref="Configuration"/>. CMS-owned: table name is <c>CMSHomePageSections</c>.</summary>
public class CMSHomePageSection : AuditableEntity, ISoftDeletable
{
    public HomePageSectionType SectionType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>Type-specific JSON configuration (max item count, source type, collection/rule/banner ids, HTML).</summary>
    public string? Configuration { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
