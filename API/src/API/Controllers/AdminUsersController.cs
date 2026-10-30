using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.AdminUsers;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage admin user accounts and the roles assigned to them.</summary>
[Route("api/admin/users")]
[RequireModule(Modules.Users)]
public class AdminUsersController : ApiControllerBase
{
    /// <summary>List users (paged), optionally filtered by email/username, active and/or email-verified state.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<AdminUserDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null, [FromQuery] bool? emailVerified = null)
        => Ok(await Mediator.Send(new ListUsersQuery(page, pageSize, search, isActive, emailVerified)));

    /// <summary>Get a single user by id, including profile and role assignments.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetUserQuery(id)));

    /// <summary>Create a new user account with the given roles.</summary>
    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create([FromBody] CreateUserCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a user's active status and profile name.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AdminUserDto>> Update(Guid id, [FromBody] UpdateUserCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Replace a user's role assignments with exactly the requested set.</summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<ActionResult<AdminUserDto>> AssignRoles(Guid id, [FromBody] AssignUserRolesCommand command)
        => Ok(await Mediator.Send(command with { UserId = id }));

    /// <summary>Set (overwrite) a user's password directly.</summary>
    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> SetPassword(Guid id, [FromBody] SetUserPasswordCommand command)
    {
        await Mediator.Send(command with { Id = id });
        return NoContent();
    }

    /// <summary>Email the user a single-use password-reset link.</summary>
    [HttpPost("{id:guid}/send-password-reset")]
    public async Task<IActionResult> SendPasswordReset(Guid id)
    {
        await Mediator.Send(new SendUserPasswordResetCommand(id));
        return NoContent();
    }

    /// <summary>Delete (soft) a user account.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteUserCommand(id));
        return NoContent();
    }
}
