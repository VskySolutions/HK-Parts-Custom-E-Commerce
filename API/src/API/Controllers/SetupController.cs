using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Authentication;
using HKPartsEcommerce.Application.Features.Setup;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>First-run setup wizard endpoints (unauthenticated; disabled once setup completes).</summary>
[Route("api/setup")]
[AllowAnonymous]
public class SetupController : ApiControllerBase
{
    /// <summary>Whether setup has been completed and whether a super admin exists.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<SetupStatusDto>> Status()
        => Ok(await Mediator.Send(new GetSetupStatusQuery()));

    /// <summary>Complete first-run setup and auto-login the new super admin.</summary>
    [HttpPost("complete")]
    public async Task<ActionResult<AuthResponse>> Complete([FromBody] CompleteSetupCommand command)
        => Ok(await Mediator.Send(command));
}
