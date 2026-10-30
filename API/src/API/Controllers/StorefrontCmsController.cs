using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.CmsBlog;
using HKPartsEcommerce.Application.Features.CmsPages;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront CMS surface (WO-54): published content pages by slug, footer/nav link
/// columns, and the published blog. Draft/Archived and soft-deleted content is never exposed.
/// </summary>
[Route("api/storefront/cms")]
[AllowAnonymous]
public class StorefrontCmsController : ApiControllerBase
{
    /// <summary>Fetch a single published content page by slug (404 when missing or unpublished).</summary>
    [HttpGet("pages/{slug}")]
    public async Task<ActionResult<CmsPageDto>> GetPage(string slug)
        => Ok(await Mediator.Send(new GetCmsPageBySlugQuery(slug)));

    /// <summary>Published pages grouped into footer/nav columns (by group then page display order).</summary>
    [HttpGet("navigation")]
    public async Task<ActionResult<IReadOnlyList<CmsNavGroupDto>>> Navigation()
        => Ok(await Mediator.Send(new GetFooterNavigationQuery()));

    /// <summary>Published pages flagged for quick-access links in the storefront top bar.</summary>
    [HttpGet("header-navigation")]
    public async Task<ActionResult<IReadOnlyList<CmsNavPageDto>>> HeaderNavigation()
        => Ok(await Mediator.Send(new GetHeaderNavigationQuery()));

    /// <summary>Paged list of published blog posts, newest first.</summary>
    [HttpGet("blog")]
    public async Task<ActionResult<PaginatedList<CmsBlogPostDto>>> Blog(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12)
        => Ok(await Mediator.Send(new ListPublishedBlogPostsQuery(page, pageSize)));

    /// <summary>Fetch a single published blog post by slug (404 when missing or unpublished).</summary>
    [HttpGet("blog/{slug}")]
    public async Task<ActionResult<CmsBlogPostDto>> GetBlogPost(string slug)
        => Ok(await Mediator.Send(new GetPublishedBlogPostBySlugQuery(slug)));
}
