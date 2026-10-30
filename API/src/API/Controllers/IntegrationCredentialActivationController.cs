using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Cross-integration quick action: toggle a credential's Active flag straight from the admin grid, without
/// re-supplying its secret fields. Dispatched by integration slug (e.g. <c>stripe</c>, <c>azure-blob</c>).
/// </summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class IntegrationCredentialActivationController : ApiControllerBase
{
    /// <summary>Activate or deactivate a credential row (activating deactivates the integration's other rows).</summary>
    [HttpPost("{provider}/{id:guid}/active")]
    public async Task<IActionResult> SetActive(string provider, Guid id, [FromBody] SetActiveRequest body)
    {
        await Mediator.Send(new SetIntegrationCredentialActiveCommand(provider, id, body.Active));
        return NoContent();
    }

    public record SetActiveRequest(bool Active);
}
