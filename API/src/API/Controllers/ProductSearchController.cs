using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.ProductSearch;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Public storefront product search, faceted filtering, and autocomplete (REQ-STF-002, REQ-STF-003).</summary>
[Route("api/storefront/search")]
[AllowAnonymous]
public class ProductSearchController : ApiControllerBase
{
    /// <summary>
    /// Search published products by keyword and filters, returning a sorted, paged result set plus
    /// facet counts. Array filters (e.g. <c>specificationOptionIds</c>) bind from repeated query params.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SearchResultsDto>> Search(
        [FromQuery] string? query,
        [FromQuery] List<Guid>? specificationOptionIds,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] Guid? manufacturerId,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new SearchProductsQuery(
            query, specificationOptionIds, minPrice, maxPrice, manufacturerId, categoryId, sort, page, pageSize)));

    /// <summary>Autocomplete suggestions: matching published product names and enabled category names.</summary>
    [HttpGet("autocomplete")]
    public async Task<ActionResult<AutocompleteResultDto>> Autocomplete(
        [FromQuery] string query, [FromQuery] int limit = 10)
        => Ok(await Mediator.Send(new AutocompleteQuery(query, limit)));
}
