using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.CmsSearchContent;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Configure the storefront search-results page (WO-105): heading, input placeholder, results-count label,
/// no-results message, and an optional no-results promotional banner/collection. Backed by a single config row.
/// </summary>
[Route("api/admin/search-page-content")]
[RequireModule(Modules.Cms)]
public class AdminSearchPageContentController : ApiControllerBase
{
    /// <summary>Get the current search-page content (or the in-code defaults when nothing is configured yet).</summary>
    [HttpGet]
    public async Task<ActionResult<CmsSearchPageContentDto>> Get()
        => Ok(await Mediator.Send(new GetSearchPageContentAdminQuery()));

    /// <summary>Create or update the singleton search-page content row.</summary>
    [HttpPut]
    public async Task<ActionResult<CmsSearchPageContentDto>> Update([FromBody] UpdateSearchPageContentCommand command)
        => Ok(await Mediator.Send(command));
}
