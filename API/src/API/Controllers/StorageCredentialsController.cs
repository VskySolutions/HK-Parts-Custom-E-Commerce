using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Storage-provider credentials (Azure Blob Storage).</summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class StorageCredentialsController : ApiControllerBase
{
    // ---- Azure Blob ----
    [HttpGet("azure-blob")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListAzureBlob()
        => Ok(await Mediator.Send(new ListAzureBlobCredentialsQuery()));

    [HttpGet("azure-blob/{id:guid}")]
    public async Task<ActionResult<AzureBlobCredentialDto>> GetAzureBlob(Guid id)
        => Ok(await Mediator.Send(new GetAzureBlobCredentialQuery(id)));

    [HttpPost("azure-blob")]
    public async Task<ActionResult<AzureBlobCredentialDto>> CreateAzureBlob([FromBody] SaveAzureBlobCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("azure-blob/{id:guid}")]
    public async Task<ActionResult<AzureBlobCredentialDto>> UpdateAzureBlob(Guid id, [FromBody] SaveAzureBlobCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("azure-blob/{id:guid}")]
    public async Task<IActionResult> DeleteAzureBlob(Guid id)
    {
        await Mediator.Send(new DeleteAzureBlobCredentialCommand(id));
        return NoContent();
    }
}
