using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.CmsHomeSections;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Manage the configurable storefront home page layout (WO-96): CRUD over home page sections, plus reordering
/// and per-section enable/disable. The single-enabled-HeroBanner invariant is enforced by the handlers.
/// </summary>
[Route("api/admin/home-sections")]
[RequireModule(Modules.Cms)]
public class AdminHomeSectionsController : ApiControllerBase
{
    /// <summary>All home page sections (enabled and disabled), in display order.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CmsHomeSectionDto>>> List()
        => Ok(await Mediator.Send(new ListHomeSectionsQuery()));

    /// <summary>Get a single home page section by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CmsHomeSectionDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetHomeSectionQuery(id)));

    /// <summary>Create a home page section.</summary>
    [HttpPost]
    public async Task<ActionResult<CmsHomeSectionDto>> Create([FromBody] CreateHomeSectionCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a home page section (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CmsHomeSectionDto>> Update(Guid id, [FromBody] UpdateHomeSectionCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Soft-delete a home page section.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteHomeSectionCommand(id));
        return NoContent();
    }

    /// <summary>Rewrite section ordering from the supplied ordered id list; returns the sections in the new order.</summary>
    [HttpPut("reorder")]
    public async Task<ActionResult<IReadOnlyList<CmsHomeSectionDto>>> Reorder([FromBody] ReorderHomeSectionsCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Enable or disable a single section (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}/enabled")]
    public async Task<ActionResult<CmsHomeSectionDto>> SetEnabled(Guid id, [FromBody] SetHomeSectionEnabledCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));
}
