using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Localization;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront language surface (AC-STF-004.2): the enabled languages a buyer may
/// select, default first. Feeds the storefront language selector.
/// </summary>
[Route("api/storefront/languages")]
[AllowAnonymous]
public class StorefrontLanguagesController : ApiControllerBase
{
    /// <summary>List the enabled display languages (default first).</summary>
    [HttpGet]
    public async Task<ActionResult<List<StorefrontLanguageDto>>> List()
        => Ok(await Mediator.Send(new GetActiveLanguagesQuery()));
}
