using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CmsBanners;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public storefront banners (WO-55): returns the enabled banners for a placement whose active date range
/// currently contains "now". Banners outside their active window are never returned here.
/// </summary>
[Route("api/storefront/banners")]
[AllowAnonymous]
public class StorefrontBannersController : ApiControllerBase
{
    /// <summary>Active banners for the given display location, e.g. <c>?location=home-hero</c>, ordered by display order.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CmsBannerPublicDto>>> Get([FromQuery] string? location = null)
        => Ok(await Mediator.Send(new GetActiveBannersQuery(location ?? string.Empty)));
}
