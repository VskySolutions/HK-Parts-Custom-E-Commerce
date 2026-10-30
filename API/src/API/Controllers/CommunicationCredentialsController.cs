using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Communication-provider credentials (Twilio SMS / WhatsApp).</summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class CommunicationCredentialsController : ApiControllerBase
{
    // ---- Twilio ----
    [HttpGet("twilio")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListTwilio()
        => Ok(await Mediator.Send(new ListTwilioCredentialsQuery()));

    [HttpGet("twilio/{id:guid}")]
    public async Task<ActionResult<TwilioCredentialDto>> GetTwilio(Guid id)
        => Ok(await Mediator.Send(new GetTwilioCredentialQuery(id)));

    [HttpPost("twilio")]
    public async Task<ActionResult<TwilioCredentialDto>> CreateTwilio([FromBody] SaveTwilioCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("twilio/{id:guid}")]
    public async Task<ActionResult<TwilioCredentialDto>> UpdateTwilio(Guid id, [FromBody] SaveTwilioCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("twilio/{id:guid}")]
    public async Task<IActionResult> DeleteTwilio(Guid id)
    {
        await Mediator.Send(new DeleteTwilioCredentialCommand(id));
        return NoContent();
    }
}
