using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CmsSearchContent;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public storefront content for the search-results page (WO-105): effective heading/placeholder/count-label/
/// no-results message plus any configured no-results banner and collection products. Always returns content.
/// </summary>
[Route("api/storefront/search-content")]
[AllowAnonymous]
public class StorefrontSearchContentController : ApiControllerBase
{
    /// <summary>Effective search-page content for the storefront, including any configured no-results banner/products.</summary>
    [HttpGet]
    public async Task<ActionResult<StorefrontSearchContentDto>> Get()
        => Ok(await Mediator.Send(new GetStorefrontSearchContentQuery()));
}
