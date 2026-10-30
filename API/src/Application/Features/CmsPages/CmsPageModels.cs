using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Full view of a CMS content page (SEO metadata, publish status, grouping).</summary>
public class CmsPageDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Body { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }

    public CmsContentStatus Status { get; set; }

    public Guid? PageGroupId { get; set; }

    /// <summary>Resolved name of the owning group (null when ungrouped or the group was removed).</summary>
    public string? PageGroupName { get; set; }

    public int DisplayOrder { get; set; }

    /// <summary>Whether the storefront shows this page as a quick-access link in its top bar.</summary>
    public bool ShowInTopBar { get; set; }

    /// <summary>Whether the storefront lists this page in the footer navigation (bottom bar).</summary>
    public bool ShowInFooter { get; set; }

    /// <summary>Pre-seeded informational pages are protected from deletion; the UI hides the delete action.</summary>
    public bool IsSystemPage { get; set; }

    public static CmsPageDto From(CMSPage p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Slug = p.Slug,
        Body = p.Body,
        MetaTitle = p.MetaTitle,
        MetaDescription = p.MetaDescription,
        MetaKeywords = p.MetaKeywords,
        CanonicalUrl = p.CanonicalUrl,
        Status = p.Status,
        PageGroupId = p.PageGroupId,
        PageGroupName = p.PageGroup?.Name,
        DisplayOrder = p.DisplayOrder,
        ShowInTopBar = p.ShowInTopBar,
        ShowInFooter = p.ShowInFooter,
        IsSystemPage = p.IsSystemPage,
    };
}
