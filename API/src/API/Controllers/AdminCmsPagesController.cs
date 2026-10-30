using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.CmsPages;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage CMS content pages (SEO metadata, grouping, publish lifecycle). System pages are
/// protected from deletion.</summary>
[Route("api/admin/cms-pages")]
[RequireModule(Modules.Cms)]
public class AdminCmsPagesController : ApiControllerBase
{
    /// <summary>List CMS pages (paged), optionally filtered by status, group and a title/slug search.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CmsPageDto>>> List(
        [FromQuery] CmsContentStatus? status = null,
        [FromQuery] Guid? pageGroupId = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListCmsPagesQuery(status, pageGroupId, search, page, pageSize, sortBy, sortDescending)));

    /// <summary>Get a single CMS page by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CmsPageDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCmsPageQuery(id)));

    /// <summary>Create a new CMS page.</summary>
    [HttpPost]
    public async Task<ActionResult<CmsPageDto>> Create([FromBody] CreateCmsPageCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing CMS page (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CmsPageDto>> Update(Guid id, [FromBody] UpdateCmsPageCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Publish / unpublish / archive a CMS page.</summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<CmsPageDto>> SetStatus(Guid id, [FromBody] SetCmsPageStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a CMS page. Returns 409 for protected system pages.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCmsPageCommand(id));
        return NoContent();
    }
}
