using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin returns management (REQ-ORD-004): review, and approve/reject with a resolution.</summary>
[Route("api/admin/rma")]
[RequireModule(Modules.Orders)]
public class AdminRmaController : ApiControllerBase
{
    /// <summary>List returns (paged), optionally filtered by status.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<RmaDto>>> List(
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? resolution = null)
        => Ok(await Mediator.Send(new ListRmasQuery(status, page, pageSize, search, resolution)));

    /// <summary>Get a single return.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RmaDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetRmaQuery(id)));

    /// <summary>Approve or reject a return and record its resolution.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RmaDto>> Resolve(Guid id, [FromBody] ResolveRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));
}
