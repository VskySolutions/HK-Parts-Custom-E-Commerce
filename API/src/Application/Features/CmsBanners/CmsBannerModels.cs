using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsBanners;

/// <summary>Full admin view of a promotional banner/slide (WO-55).</summary>
public class CmsBannerDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }

    /// <summary>Central Media asset id for the banner image.</summary>
    public Guid? ImageMediaId { get; set; }

    /// <summary>Resolved image URL from the linked Media asset (null when no image is set).</summary>
    public string? ImageUrl { get; set; }

    public string? LinkUrl { get; set; }
    public string? CtaLabel { get; set; }

    /// <summary>Placement key, e.g. "home-hero", "home-double", "category", "search-no-results".</summary>
    public string DisplayLocation { get; set; } = string.Empty;

    public DateTime? StartsOnUtc { get; set; }
    public DateTime? EndsOnUtc { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsEnabled { get; set; }

    public static CmsBannerDto From(CMSBanner b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Subtitle = b.Subtitle,
        ImageMediaId = b.ImageMediaId,
        ImageUrl = b.ImageMedia?.Url,
        LinkUrl = b.LinkUrl,
        CtaLabel = b.CtaLabel,
        DisplayLocation = b.DisplayLocation,
        StartsOnUtc = b.StartsOnUtc,
        EndsOnUtc = b.EndsOnUtc,
        DisplayOrder = b.DisplayOrder,
        IsEnabled = b.IsEnabled,
    };
}

/// <summary>
/// Public storefront projection of an active banner: only the fields a buyer's client needs to render a
/// slide. Surfaced solely by the storefront endpoint, which filters out any banner outside its active
/// date range.
/// </summary>
public class CmsBannerPublicDto
{
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? LinkUrl { get; set; }
    public string? CtaLabel { get; set; }

    public static CmsBannerPublicDto From(CMSBanner b) => new()
    {
        Title = b.Title,
        Subtitle = b.Subtitle,
        ImageUrl = b.ImageMedia?.Url,
        LinkUrl = b.LinkUrl,
        CtaLabel = b.CtaLabel,
    };
}
