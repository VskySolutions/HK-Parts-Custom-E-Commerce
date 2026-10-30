using System.Text.Json;
using System.Text.Json.Serialization;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// SEO surface (WO-57): builds the storefront sitemap.xml (published products, categories, CMS pages and
/// blog posts), serves the DB-backed robots.txt (or a sensible default), and produces schema.org Product
/// JSON-LD for product detail pages. The sitemap XML is memory-cached with a short, configurable TTL
/// (default 15 min) per the Content &amp; Marketing blueprint ADR.
/// </summary>
public interface ISeoService
{
    /// <summary>
    /// Returns a valid <c>urlset</c> sitemap.xml listing published products, enabled categories, published
    /// CMS pages and published blog posts (absolute <c>loc</c>, <c>lastmod</c> where available). The result
    /// is memory-cached with a configurable TTL.
    /// </summary>
    Task<string> GenerateSitemapXmlAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the DB-backed robots.txt (settings key <see cref="SeoSettingKeys.RobotsTxt"/>), or a sensible
    /// default (allow all + <c>Sitemap: {base}/sitemap.xml</c>) when the setting is unset/blank.
    /// </summary>
    Task<string> GetRobotsTxtAsync(CancellationToken cancellationToken = default);

    /// <summary>Builds schema.org Product JSON-LD data (name, description, sku, image, offers) for a product.</summary>
    ProductSchemaMarkup BuildProductSchema(ProductSchemaInput product);

    /// <summary>Cache-derived sitemap status (last-generated time + entry count); does not force generation.</summary>
    SeoSitemapStatus GetSitemapStatus();

    /// <summary>Drops the cached sitemap so the next request (or an admin refresh) regenerates it.</summary>
    void InvalidateSitemapCache();
}

/// <summary>Platform-setting keys owned by the SEO service.</summary>
public static class SeoSettingKeys
{
    /// <summary>Custom robots.txt body. When null/blank the service serves a computed default.</summary>
    public const string RobotsTxt = "seo.robots-txt";
}

/// <summary>
/// Input for <see cref="ISeoService.BuildProductSchema"/>. The service owns URL construction (absolute
/// image URL + canonical product URL) from the configured storefront base URL, so callers pass the raw
/// (possibly relative) media URL and the product slug.
/// </summary>
public record ProductSchemaInput(
    string Name,
    string? Description,
    string? Sku,
    string? Slug,
    string? ImageUrl,
    decimal? Price,
    string PriceCurrency,
    bool InStock,
    string? Brand = null);

/// <summary>Cache-derived sitemap generation status.</summary>
public record SeoSitemapStatus(DateTime? GeneratedOnUtc, int EntryCount, bool IsCached);

/// <summary>
/// schema.org Product JSON-LD (serializes with <c>@context</c>/<c>@type</c>). A product detail response can
/// embed <see cref="ToJson"/> inside a <c>&lt;script type="application/ld+json"&gt;</c> block.
/// </summary>
public class ProductSchemaMarkup
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    [JsonPropertyName("@context")] public string Context { get; set; } = "https://schema.org";
    [JsonPropertyName("@type")] public string Type { get; set; } = "Product";
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("sku")] public string? Sku { get; set; }
    [JsonPropertyName("image")] public string? Image { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("brand")] public SchemaBrand? Brand { get; set; }
    [JsonPropertyName("offers")] public SchemaOffer? Offers { get; set; }

    /// <summary>Serializes to a compact JSON-LD string (null members omitted).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

/// <summary>schema.org Brand node.</summary>
public class SchemaBrand
{
    [JsonPropertyName("@type")] public string Type { get; set; } = "Brand";
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

/// <summary>schema.org Offer node (price as an invariant-culture string, ISO-4217 currency, availability URL).</summary>
public class SchemaOffer
{
    [JsonPropertyName("@type")] public string Type { get; set; } = "Offer";
    [JsonPropertyName("price")] public string? Price { get; set; }
    [JsonPropertyName("priceCurrency")] public string? PriceCurrency { get; set; }
    [JsonPropertyName("availability")] public string? Availability { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
}
