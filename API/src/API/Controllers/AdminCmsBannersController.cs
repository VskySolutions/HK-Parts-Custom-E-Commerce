using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.CmsBanners;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage promotional banners/slides (WO-55): image, placement, active date range, ordering, enable/disable.</summary>
[Route("api/admin/banners")]
[RequireModule(Modules.Cms)]
public class AdminCmsBannersController : ApiControllerBase
{
    /// <summary>List banners (paged), optionally filtered by display location, enabled state, and title.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CmsBannerDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? displayLocation = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? search = null)
        => Ok(await Mediator.Send(new ListCmsBannersQuery(page, pageSize, displayLocation, isEnabled, search)));

    /// <summary>Get a single banner by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CmsBannerDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCmsBannerQuery(id)));

    /// <summary>Create a new banner.</summary>
    [HttpPost]
    public async Task<ActionResult<CmsBannerDto>> Create([FromBody] CreateCmsBannerCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing banner (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CmsBannerDto>> Update(Guid id, [FromBody] UpdateCmsBannerCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a banner.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCmsBannerCommand(id));
        return NoContent();
    }
}
