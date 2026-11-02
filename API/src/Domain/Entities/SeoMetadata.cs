using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Search-engine / social-share metadata for any storefront-addressable record, stored centrally and keyed
/// by <see cref="EntityType"/> + <see cref="EntityId"/> (one row per record). The record's URL slug stays on
/// the owning entity (it is its routing identity); everything that only shapes the rendered page head lives
/// here. Every field is optional — the storefront falls back to the record's own name/description.
/// </summary>
public class SeoMetadata : AuditableEntity
{
    public SeoEntityType EntityType { get; set; }

    /// <summary>Id of the owning record (no FK — the owner lives in a different table per entity type).</summary>
    public Guid EntityId { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }

    /// <summary>Page heading override; the storefront falls back to the record's name/title.</summary>
    public string? H1 { get; set; }

    /// <summary>Social-share (og:image) picture from the media library.</summary>
    public Guid? MetaImageMediaId { get; set; }
    public Media? MetaImage { get; set; }

    /// <summary>Raw Open Graph markup that replaces the generated og: tags.</summary>
    public string? MetaOpenGraph { get; set; }
    /// <summary>Raw tags/scripts injected into the page &lt;head&gt;.</summary>
    public string? MetaCustomTags { get; set; }
    /// <summary>Raw tags/scripts injected before &lt;/body&gt;.</summary>
    public string? MetaFooterTags { get; set; }

    /// <summary>Emit <c>robots: noindex</c> and leave the record out of sitemap.xml.</summary>
    public bool NoIndex { get; set; }
}
