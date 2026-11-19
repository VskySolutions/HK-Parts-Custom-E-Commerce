using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Tax-provider credentials (TaxJar, Stripe Tax).</summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class TaxCredentialsController : ApiControllerBase
{
    // ---- TaxJar ----
    [HttpGet("taxjar")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListTaxJar()
        => Ok(await Mediator.Send(new ListTaxJarCredentialsQuery()));

    [HttpGet("taxjar/{id:guid}")]
    public async Task<ActionResult<TaxJarCredentialDto>> GetTaxJar(Guid id)
        => Ok(await Mediator.Send(new GetTaxJarCredentialQuery(id)));

    [HttpPost("taxjar")]
    public async Task<ActionResult<TaxJarCredentialDto>> CreateTaxJar([FromBody] SaveTaxJarCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("taxjar/{id:guid}")]
    public async Task<ActionResult<TaxJarCredentialDto>> UpdateTaxJar(Guid id, [FromBody] SaveTaxJarCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("taxjar/{id:guid}")]
    public async Task<IActionResult> DeleteTaxJar(Guid id)
    {
        await Mediator.Send(new DeleteTaxJarCredentialCommand(id));
        return NoContent();
    }

    // ---- Stripe Tax ----
    [HttpGet("stripe-tax")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListStripeTax()
        => Ok(await Mediator.Send(new ListStripeTaxCredentialsQuery()));

    [HttpGet("stripe-tax/{id:guid}")]
    public async Task<ActionResult<StripeTaxCredentialDto>> GetStripeTax(Guid id)
        => Ok(await Mediator.Send(new GetStripeTaxCredentialQuery(id)));

    [HttpPost("stripe-tax")]
    public async Task<ActionResult<StripeTaxCredentialDto>> CreateStripeTax([FromBody] SaveStripeTaxCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("stripe-tax/{id:guid}")]
    public async Task<ActionResult<StripeTaxCredentialDto>> UpdateStripeTax(Guid id, [FromBody] SaveStripeTaxCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("stripe-tax/{id:guid}")]
    public async Task<IActionResult> DeleteStripeTax(Guid id)
    {
        await Mediator.Send(new DeleteStripeTaxCredentialCommand(id));
        return NoContent();
    }

    // ---- Avalara (AvaTax v2: address verification, AV-21) ----
    [HttpGet("avalara")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListAvalara()
        => Ok(await Mediator.Send(new ListAvalaraCredentialsQuery()));

    [HttpGet("avalara/{id:guid}")]
    public async Task<ActionResult<AvalaraCredentialDto>> GetAvalara(Guid id)
        => Ok(await Mediator.Send(new GetAvalaraCredentialQuery(id)));

    [HttpPost("avalara")]
    public async Task<ActionResult<AvalaraCredentialDto>> CreateAvalara([FromBody] SaveAvalaraCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("avalara/{id:guid}")]
    public async Task<ActionResult<AvalaraCredentialDto>> UpdateAvalara(Guid id, [FromBody] SaveAvalaraCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("avalara/{id:guid}")]
    public async Task<IActionResult> DeleteAvalara(Guid id)
    {
        await Mediator.Send(new DeleteAvalaraCredentialCommand(id));
        return NoContent();
    }
}
