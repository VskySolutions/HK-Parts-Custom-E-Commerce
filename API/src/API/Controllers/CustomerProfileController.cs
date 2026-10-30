using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CustomerProfile;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's own profile and credential management (REQ-CUS-002).</summary>
[Route("api/customer/profile")]
[Authorize]
public class CustomerProfileController : ApiControllerBase
{
    /// <summary>Get the current customer's profile.</summary>
    [HttpGet]
    public async Task<ActionResult<CustomerProfileDto>> Get()
        => Ok(await Mediator.Send(new GetMyProfileQuery()));

    /// <summary>Update the current customer's name and phone number.</summary>
    [HttpPut]
    public async Task<ActionResult<CustomerProfileDto>> Update([FromBody] UpdateMyProfileCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Change the current customer's login email (triggers re-verification).</summary>
    [HttpPut("email")]
    public async Task<IActionResult> ChangeEmail([FromBody] ChangeMyEmailCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }

    /// <summary>Change the current customer's password.</summary>
    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangeMyPasswordCommand command)
    {
        await Mediator.Send(command);
        return NoContent();
    }
}
