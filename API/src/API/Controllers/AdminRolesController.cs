using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Roles;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage custom roles and the admin modules they grant. System roles are read-only.</summary>
[Route("api/admin/roles")]
[RequireModule(Modules.Roles)]
public class AdminRolesController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List()
        => Ok(await Mediator.Send(new ListRolesQuery()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetRoleQuery(id)));

    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Update(Guid id, [FromBody] UpdateRoleCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteRoleCommand(id));
        return NoContent();
    }

    /// <summary>Catalog of admin modules a role can be granted, for populating the role editor.</summary>
    [HttpGet("modules")]
    public async Task<ActionResult<IReadOnlyList<ModuleInfo>>> GetModules()
        => Ok(await Mediator.Send(new GetModulesQuery()));
}
