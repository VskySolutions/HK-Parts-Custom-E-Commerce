using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Admin view of a record's central SEO metadata. Returned even when no row exists yet (all fields empty,
/// <see cref="Exists"/> false), so the shared SEO panel can always bind to it.
/// </summary>
public class SeoMetadataDto
{
    public SeoEntityType EntityType { get; set; }
    public Guid EntityId { get; set; }
    public bool Exists { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? H1 { get; set; }
    public Guid? MetaImageMediaId { get; set; }
    public string? MetaImageUrl { get; set; }
    public string? MetaOpenGraph { get; set; }
    public string? MetaCustomTags { get; set; }
    public string? MetaFooterTags { get; set; }
    public bool NoIndex { get; set; }

    public static SeoMetadataDto Empty(SeoEntityType type, Guid id) => new() { EntityType = type, EntityId = id };

    public static SeoMetadataDto From(SeoMetadata s) => new()
    {
        EntityType = s.EntityType,
        EntityId = s.EntityId,
        Exists = true,
        MetaTitle = s.MetaTitle,
        MetaDescription = s.MetaDescription,
        MetaKeywords = s.MetaKeywords,
        CanonicalUrl = s.CanonicalUrl,
        H1 = s.H1,
        MetaImageMediaId = s.MetaImageMediaId,
        MetaImageUrl = s.MetaImage?.Url,
        MetaOpenGraph = s.MetaOpenGraph,
        MetaCustomTags = s.MetaCustomTags,
        MetaFooterTags = s.MetaFooterTags,
        NoIndex = s.NoIndex,
    };
}

/// <summary>
/// Public storefront SEO payload embedded in detail responses. Deliberately excludes the raw admin-authored
/// HTML fields (Open Graph override, custom head/footer tags), which the storefront does not render.
/// Null fields mean "fall back to the record's own name/description".
/// </summary>
public class StorefrontSeoDto
{
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }
    public string? CanonicalUrl { get; set; }
    public string? H1 { get; set; }
    public string? MetaImageUrl { get; set; }
    public bool NoIndex { get; set; }

    public static StorefrontSeoDto From(SeoMetadata? s) => s is null ? new() : new()
    {
        MetaTitle = s.MetaTitle,
        MetaDescription = s.MetaDescription,
        MetaKeywords = s.MetaKeywords,
        CanonicalUrl = s.CanonicalUrl,
        H1 = s.H1,
        MetaImageUrl = s.MetaImage?.Url,
        NoIndex = s.NoIndex,
    };
}
