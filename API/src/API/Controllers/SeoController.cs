using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Seo;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public SEO endpoints (WO-57): the storefront <c>sitemap.xml</c>, <c>robots.txt</c>, and per-product
/// schema.org JSON-LD. The leading-slash action routes are absolute and escape the
/// <see cref="ApiControllerBase"/> <c>api/[controller]</c> prefix, so the sitemap and robots files sit at
/// the site root where crawlers expect them.
/// </summary>
[AllowAnonymous]
public class SeoController : ApiControllerBase
{
    /// <summary>The storefront sitemap.xml (published products, enabled categories, published CMS pages and blog posts).</summary>
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
        => Content(await Mediator.Send(new GetSitemapQuery()), "application/xml");

    /// <summary>The robots.txt served to crawlers (DB-backed custom body, or a sensible default).</summary>
    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots()
        => Content(await Mediator.Send(new GetRobotsTxtQuery()), "text/plain");

    /// <summary>schema.org Product JSON-LD for a published product (by slug or id) — for embedding on the detail page.</summary>
    [HttpGet("/api/storefront/products/{slug}/schema")]
    public async Task<IActionResult> ProductSchema(string slug)
    {
        var markup = await Mediator.Send(new GetProductSchemaQuery(slug));
        return Content(markup.ToJson(), "application/ld+json");
    }
}
