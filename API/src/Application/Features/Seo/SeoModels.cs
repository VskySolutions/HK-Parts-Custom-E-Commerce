namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Admin SEO settings view: the effective robots.txt plus whether it is customised.</summary>
public class SeoSettingsDto
{
    /// <summary>The robots.txt currently served at <c>/robots.txt</c> (custom body, or the computed default).</summary>
    public string RobotsTxt { get; set; } = string.Empty;

    /// <summary>True when a custom body is stored; false when the built-in default is being served.</summary>
    public bool IsCustomRobotsTxt { get; set; }
}

/// <summary>Admin sitemap status: cache-derived last-generated time + entry count.</summary>
public class SitemapStatusDto
{
    /// <summary>When the currently-cached sitemap was generated (null when nothing is cached).</summary>
    public DateTime? GeneratedOnUtc { get; set; }

    /// <summary>Number of URLs in the currently-cached sitemap (0 when nothing is cached).</summary>
    public int EntryCount { get; set; }

    /// <summary>True when a generated sitemap is currently cached.</summary>
    public bool IsCached { get; set; }
}
