using System.Globalization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Seo;

/// <summary>
/// Builds the storefront sitemap.xml (published products / enabled categories / published CMS pages and
/// blog posts), serves the DB-backed robots.txt (or a computed default), and produces schema.org Product
/// JSON-LD. The sitemap XML is memory-cached with a short, configurable TTL
/// (<c>Seo:SitemapCacheMinutes</c>, default 15) per the Content &amp; Marketing blueprint ADR.
/// </summary>
public class SeoService : ISeoService
{
    internal const string SitemapCacheKey = "seo:sitemap";
    private const string SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const int DefaultCacheMinutes = 15;

    // Storefront URL path templates: an absolute loc is base + template + slug. The "/shop/*" prefix matches
    // the storefront SPA (WEB storefront routes.js). NOTE: product & category routes exist today; the page
    // and blog templates are the assumed convention (the SPA has no dedicated route yet) — adjust here if the
    // storefront settles on different paths.
    private const string ProductPath = "/shop/product/";
    private const string CategoryPath = "/shop/category/";
    private const string PagePath = "/shop/page/";
    private const string BlogPath = "/shop/blog/";

    private const string InStockUrl = "https://schema.org/InStock";
    private const string OutOfStockUrl = "https://schema.org/OutOfStock";

    private readonly IApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;
    private readonly string _baseUrl;
    private readonly TimeSpan _cacheTtl;

    public SeoService(
        IApplicationDbContext db,
        IMemoryCache cache,
        ISettingsService settings,
        IDateTimeProvider clock,
        IConfiguration configuration)
    {
        _db = db;
        _cache = cache;
        _settings = settings;
        _clock = clock;

        // Same source the storefront email-link builder uses (Storefront:PublicBaseUrl). IStorefrontUrlBuilder
        // exposes no base-url getter, so we read the shared config key directly and fall back to the dev origin.
        var configured = configuration["Storefront:PublicBaseUrl"];
        _baseUrl = string.IsNullOrWhiteSpace(configured) ? "http://localhost:9000" : configured.TrimEnd('/');

        // Read the TTL via the indexer (no Configuration.Binder dependency in Infrastructure).
        var minutes = int.TryParse(configuration["Seo:SitemapCacheMinutes"], out var m) && m > 0 ? m : DefaultCacheMinutes;
        _cacheTtl = TimeSpan.FromMinutes(minutes);
    }

    private sealed record SitemapCacheEntry(string Xml, DateTime GeneratedOnUtc, int EntryCount);

    public async Task<string> GenerateSitemapXmlAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(SitemapCacheKey, out SitemapCacheEntry? cached) && cached is not null)
            return cached.Xml;

        var entry = await BuildSitemapAsync(cancellationToken);
        _cache.Set(SitemapCacheKey, entry, _cacheTtl);
        return entry.Xml;
    }

    public SeoSitemapStatus GetSitemapStatus() =>
        _cache.TryGetValue(SitemapCacheKey, out SitemapCacheEntry? cached) && cached is not null
            ? new SeoSitemapStatus(cached.GeneratedOnUtc, cached.EntryCount, IsCached: true)
            : new SeoSitemapStatus(null, 0, IsCached: false);

    public void InvalidateSitemapCache() => _cache.Remove(SitemapCacheKey);

    public async Task<string> GetRobotsTxtAsync(CancellationToken cancellationToken = default)
    {
        var custom = await _settings.GetValueAsync(SeoSettingKeys.RobotsTxt, cancellationToken);
        if (!string.IsNullOrWhiteSpace(custom))
            return custom;

        // Default: allow all crawlers + advertise the sitemap. "\n" line endings keep the served file stable
        // across platforms.
        return string.Join('\n',
            "User-agent: *",
            "Allow: /",
            "",
            $"Sitemap: {_baseUrl}/sitemap.xml",
            "");
    }

    public ProductSchemaMarkup BuildProductSchema(ProductSchemaInput product)
    {
        var url = string.IsNullOrWhiteSpace(product.Slug) ? null : Absolute(ProductPath + product.Slug!.Trim());

        var markup = new ProductSchemaMarkup
        {
            Name = product.Name,
            Description = product.Description,
            Sku = product.Sku,
            Image = ToAbsoluteUrl(product.ImageUrl),
            Url = url,
            Brand = string.IsNullOrWhiteSpace(product.Brand) ? null : new SchemaBrand { Name = product.Brand!.Trim() },
        };

        if (product.Price is decimal price)
        {
            markup.Offers = new SchemaOffer
            {
                Price = price.ToString("0.00", CultureInfo.InvariantCulture),
                PriceCurrency = string.IsNullOrWhiteSpace(product.PriceCurrency)
                    ? "USD"
                    : product.PriceCurrency.Trim().ToUpperInvariant(),
                Availability = product.InStock ? InStockUrl : OutOfStockUrl,
                Url = url,
            };
        }

        return markup;
    }

    // ---- sitemap building -------------------------------------------------

    private async Task<SitemapCacheEntry> BuildSitemapAsync(CancellationToken ct)
    {
        // Soft-deleted rows are excluded by each entity's global query filter.
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.IsPublished && p.Slug != null && p.Slug != "")
            .Select(p => new { Slug = p.Slug!, LastMod = p.UpdatedOnUtc })
            .ToListAsync(ct);

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsEnabled && c.Slug != null && c.Slug != "")
            .Select(c => new { Slug = c.Slug!, LastMod = c.UpdatedOnUtc })
            .ToListAsync(ct);

        var pages = await _db.CMSPages.AsNoTracking()
            .Where(p => p.Status == CmsContentStatus.Published && p.Slug != "")
            .Select(p => new { p.Slug, LastMod = p.UpdatedOnUtc })
            .ToListAsync(ct);

        var posts = await _db.CMSBlogPosts.AsNoTracking()
            .Where(b => b.Status == CmsContentStatus.Published && b.Slug != "")
            .Select(b => new { b.Slug, LastMod = b.PublishedOnUtc ?? b.UpdatedOnUtc })
            .ToListAsync(ct);

        XNamespace ns = SitemapNamespace;
        var urlset = new XElement(ns + "urlset");

        // Home page (site root).
        urlset.Add(UrlElement(ns, _baseUrl + "/", null));

        foreach (var p in products) urlset.Add(UrlElement(ns, Absolute(ProductPath + p.Slug), p.LastMod));
        foreach (var c in categories) urlset.Add(UrlElement(ns, Absolute(CategoryPath + c.Slug), c.LastMod));
        foreach (var p in pages) urlset.Add(UrlElement(ns, Absolute(PagePath + p.Slug), p.LastMod));
        foreach (var b in posts) urlset.Add(UrlElement(ns, Absolute(BlogPath + b.Slug), b.LastMod));

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), urlset);
        var xml = (doc.Declaration?.ToString() ?? "<?xml version=\"1.0\" encoding=\"utf-8\"?>")
                  + Environment.NewLine + doc.ToString();

        var count = 1 + products.Count + categories.Count + pages.Count + posts.Count;
        return new SitemapCacheEntry(xml, _clock.UtcNow, count);
    }

    private static XElement UrlElement(XNamespace ns, string loc, DateTime? lastModUtc)
    {
        var url = new XElement(ns + "url", new XElement(ns + "loc", loc));
        if (lastModUtc is DateTime dt)
            url.Add(new XElement(ns + "lastmod",
                DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture)));
        return url;
    }

    private string Absolute(string relativePath) => _baseUrl + relativePath;

    private string? ToAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        // Denormalized media URLs may be stored relative (local storage). Absolutize against the storefront
        // base so the JSON-LD carries a resolvable image URL. (CDN/Azure URLs are already absolute above.)
        return _baseUrl + "/" + url.TrimStart('/');
    }
}
