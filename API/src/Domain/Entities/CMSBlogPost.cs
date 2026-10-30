using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A blog/news article with a featured image, tags, author, publish date and SEO metadata
/// (WO-54). CMS-owned: table name is <c>CMSBlogPosts</c>.</summary>
public class CMSBlogPost : AuditableEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    public Guid? FeaturedImageMediaId { get; set; }
    public Media? FeaturedImageMedia { get; set; }

    public CmsContentStatus Status { get; set; } = CmsContentStatus.Draft;
    public DateTime? PublishedOnUtc { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
