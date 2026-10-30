using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CmsCategoryConfig;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront read for a category's dynamic page configuration (WO-99): banner,
/// promotional description, published pinned products and the "You May Also Like" collection. Called
/// alongside the regular category listing; returns an empty payload (never a 404) when a category has no
/// config so the plain product grid still renders.
/// </summary>
[Route("api/storefront/category-config")]
[AllowAnonymous]
public class StorefrontCategoryConfigController : ApiControllerBase
{
    /// <summary>The category's dynamic page configuration (empty payload when unconfigured or unknown).</summary>
    [HttpGet("{categoryId:guid}")]
    public async Task<ActionResult<StorefrontCategoryConfigDto>> Get(Guid categoryId)
        => Ok(await Mediator.Send(new GetStorefrontCategoryConfigQuery(categoryId)));
}
