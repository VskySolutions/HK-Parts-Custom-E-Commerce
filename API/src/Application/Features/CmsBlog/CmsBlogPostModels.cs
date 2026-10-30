using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Full view of a blog/news post (content, tags, featured image, publish status and SEO).</summary>
public class CmsBlogPostDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? Body { get; set; }
    public string? Author { get; set; }

    /// <summary>Comma-separated tags.</summary>
    public string? Tags { get; set; }

    public Guid? FeaturedImageMediaId { get; set; }

    /// <summary>Resolved featured-image URL from the central Media asset (null when none is set).</summary>
    public string? FeaturedImageUrl { get; set; }

    public CmsContentStatus Status { get; set; }
    public DateTime? PublishedOnUtc { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public static CmsBlogPostDto From(CMSBlogPost p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Slug = p.Slug,
        Summary = p.Summary,
        Body = p.Body,
        Author = p.Author,
        Tags = p.Tags,
        FeaturedImageMediaId = p.FeaturedImageMediaId,
        FeaturedImageUrl = p.FeaturedImageMedia?.Url,
        Status = p.Status,
        PublishedOnUtc = p.PublishedOnUtc,
        MetaTitle = p.MetaTitle,
        MetaDescription = p.MetaDescription,
    };
}
