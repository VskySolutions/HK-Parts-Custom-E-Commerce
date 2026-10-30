using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.StorefrontCatalog;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront catalog surface (WO-17): category pages, product detail, recently
/// viewed, product comparison, and tag / manufacturer browsing. Disabled categories and unpublished
/// products are excluded from every response.
/// </summary>
[Route("api/storefront/catalog")]
[AllowAnonymous]
public class StorefrontCatalogController : ApiControllerBase
{
    /// <summary>The enabled category tree (nested) with published-product counts — powers the header menu and home category grid.</summary>
    [HttpGet("categories")]
    public async Task<ActionResult<List<StorefrontCategoryNodeDto>>> Categories()
        => Ok(await Mediator.Send(new GetStorefrontCategoriesQuery()));

    /// <summary>Category page for an enabled category (by id or slug): paged published products plus filterable spec attributes.</summary>
    [HttpGet("category/{idOrSlug}")]
    public async Task<ActionResult<CategoryPageDto>> GetCategory(
        string idOrSlug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null)
        => Ok(await Mediator.Send(new GetCategoryPageQuery(idOrSlug, page, pageSize, sort)));

    /// <summary>Full published product detail (by id or slug): variants, media, specs and relationship sections.</summary>
    [HttpGet("product/{idOrSlug}")]
    public async Task<ActionResult<StorefrontProductDetailDto>> GetProduct(string idOrSlug)
        => Ok(await Mediator.Send(new GetProductDetailQuery(idOrSlug)));

    /// <summary>Resolves a client-maintained list of recently-viewed product ids to summaries (published only, order preserved).</summary>
    [HttpPost("recently-viewed")]
    public async Task<ActionResult<IReadOnlyList<StorefrontProductSummaryDto>>> RecentlyViewed([FromBody] List<Guid> productIds)
        => Ok(await Mediator.Send(new GetRecentlyViewedQuery(productIds)));

    /// <summary>Builds a side-by-side comparison (specifications + prices) for the given product ids.</summary>
    [HttpPost("compare")]
    public async Task<ActionResult<ComparisonDto>> Compare([FromBody] List<Guid> productIds)
        => Ok(await Mediator.Send(new CompareProductsQuery(productIds)));

    /// <summary>Paged published products sharing a tag (resolved by slug or name).</summary>
    [HttpGet("tag/{tagSlug}")]
    public async Task<ActionResult<PaginatedList<StorefrontProductSummaryDto>>> ByTag(
        string tagSlug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null)
        => Ok(await Mediator.Send(new GetProductsByTagQuery(tagSlug, page, pageSize, sort)));

    /// <summary>Paged published products for an enabled manufacturer (by id or slug).</summary>
    [HttpGet("manufacturer/{idOrSlug}")]
    public async Task<ActionResult<PaginatedList<StorefrontProductSummaryDto>>> ByManufacturer(
        string idOrSlug,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sort = null)
        => Ok(await Mediator.Send(new BrowseByManufacturerQuery(idOrSlug, page, pageSize, sort)));
}
