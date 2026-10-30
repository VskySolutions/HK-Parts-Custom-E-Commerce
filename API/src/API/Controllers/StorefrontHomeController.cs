using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CmsHomeSections;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront home page (backend half of WO-100): the ordered, enabled home page sections
/// with each section's data resolved by type. An empty section list is returned as a valid empty-state home.
/// </summary>
[Route("api/storefront/home")]
[AllowAnonymous]
public class StorefrontHomeController : ApiControllerBase
{
    /// <summary>The resolved, ordered, enabled home page sections for storefront rendering.</summary>
    [HttpGet]
    public async Task<ActionResult<StorefrontHomeDto>> Get()
        => Ok(await Mediator.Send(new GetStorefrontHomeQuery()));
}
