using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A promotional banner/slide filtered by active date range and display location (WO-55).
/// CMS-owned: table name is <c>CMSBanners</c>.</summary>
public class CMSBanner : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    public Guid? ImageMediaId { get; set; }
    public Media? ImageMedia { get; set; }

    /// <summary>CTA hyperlink target.</summary>
    public string? LinkUrl { get; set; }
    public string? CtaLabel { get; set; }

    /// <summary>Placement key, e.g. "home-hero", "home-double", "category", "search-no-results".</summary>
    public string DisplayLocation { get; set; } = string.Empty;

    public DateTime? StartsOnUtc { get; set; }
    public DateTime? EndsOnUtc { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; } = true;

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
