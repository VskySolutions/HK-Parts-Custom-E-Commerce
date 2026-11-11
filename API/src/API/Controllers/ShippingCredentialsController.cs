using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Shipping-carrier credentials (FedEx, DHL Express, USPS).</summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class ShippingCredentialsController : ApiControllerBase
{
    // ---- FedEx ----
    [HttpGet("fedex")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListFedEx()
        => Ok(await Mediator.Send(new ListFedExCredentialsQuery()));

    [HttpGet("fedex/{id:guid}")]
    public async Task<ActionResult<FedExCredentialDto>> GetFedEx(Guid id)
        => Ok(await Mediator.Send(new GetFedExCredentialQuery(id)));

    [HttpPost("fedex")]
    public async Task<ActionResult<FedExCredentialDto>> CreateFedEx([FromBody] SaveFedExCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("fedex/{id:guid}")]
    public async Task<ActionResult<FedExCredentialDto>> UpdateFedEx(Guid id, [FromBody] SaveFedExCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>FX-40: rate a sample 5 lb residential parcel 28270 → 10001; reports token status.</summary>
    [HttpPost("fedex/{id:guid}/test")]
    public async Task<ActionResult<CarrierTestResult>> TestFedEx(Guid id)
        => Ok(await Mediator.Send(new TestFedExCredentialCommand(id)));

    [HttpDelete("fedex/{id:guid}")]
    public async Task<IActionResult> DeleteFedEx(Guid id)
    {
        await Mediator.Send(new DeleteFedExCredentialCommand(id));
        return NoContent();
    }

    // ---- DHL Express ----
    [HttpGet("dhl")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListDhl()
        => Ok(await Mediator.Send(new ListDhlExpressCredentialsQuery()));

    [HttpGet("dhl/{id:guid}")]
    public async Task<ActionResult<DhlExpressCredentialDto>> GetDhl(Guid id)
        => Ok(await Mediator.Send(new GetDhlExpressCredentialQuery(id)));

    [HttpPost("dhl")]
    public async Task<ActionResult<DhlExpressCredentialDto>> CreateDhl([FromBody] SaveDhlExpressCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("dhl/{id:guid}")]
    public async Task<ActionResult<DhlExpressCredentialDto>> UpdateDhl(Guid id, [FromBody] SaveDhlExpressCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("dhl/{id:guid}")]
    public async Task<IActionResult> DeleteDhl(Guid id)
    {
        await Mediator.Send(new DeleteDhlExpressCredentialCommand(id));
        return NoContent();
    }

    // ---- USPS ----
    [HttpGet("usps")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListUsps()
        => Ok(await Mediator.Send(new ListUspsCredentialsQuery()));

    [HttpGet("usps/{id:guid}")]
    public async Task<ActionResult<UspsCredentialDto>> GetUsps(Guid id)
        => Ok(await Mediator.Send(new GetUspsCredentialQuery(id)));

    [HttpPost("usps")]
    public async Task<ActionResult<UspsCredentialDto>> CreateUsps([FromBody] SaveUspsCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("usps/{id:guid}")]
    public async Task<ActionResult<UspsCredentialDto>> UpdateUsps(Guid id, [FromBody] SaveUspsCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>US-40: rate a sample domestic (28270 → 10001) and international (→ Canada) 2 lb parcel.</summary>
    [HttpPost("usps/{id:guid}/test")]
    public async Task<ActionResult<CarrierTestResult>> TestUsps(Guid id)
        => Ok(await Mediator.Send(new TestUspsCredentialCommand(id)));

    [HttpDelete("usps/{id:guid}")]
    public async Task<IActionResult> DeleteUsps(Guid id)
    {
        await Mediator.Send(new DeleteUspsCredentialCommand(id));
        return NoContent();
    }

    // ---- UPS ----
    [HttpGet("ups")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListUps()
        => Ok(await Mediator.Send(new ListUpsCredentialsQuery()));

    [HttpGet("ups/{id:guid}")]
    public async Task<ActionResult<UpsCredentialDto>> GetUps(Guid id)
        => Ok(await Mediator.Send(new GetUpsCredentialQuery(id)));

    [HttpPost("ups")]
    public async Task<ActionResult<UpsCredentialDto>> CreateUps([FromBody] SaveUpsCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("ups/{id:guid}")]
    public async Task<ActionResult<UpsCredentialDto>> UpdateUps(Guid id, [FromBody] SaveUpsCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>UP-40/41: rate a sample 5 lb residential parcel 28270 → 10001; reports token status.</summary>
    [HttpPost("ups/{id:guid}/test")]
    public async Task<ActionResult<CarrierTestResult>> TestUps(Guid id)
        => Ok(await Mediator.Send(new TestUpsCredentialCommand(id)));

    [HttpDelete("ups/{id:guid}")]
    public async Task<IActionResult> DeleteUps(Guid id)
    {
        await Mediator.Send(new DeleteUpsCredentialCommand(id));
        return NoContent();
    }

    // ---- Freightview (LTL) ----
    [HttpGet("freightview")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListFreightview()
        => Ok(await Mediator.Send(new ListFreightviewCredentialsQuery()));

    [HttpGet("freightview/{id:guid}")]
    public async Task<ActionResult<FreightviewCredentialDto>> GetFreightview(Guid id)
        => Ok(await Mediator.Send(new GetFreightviewCredentialQuery(id)));

    [HttpPost("freightview")]
    public async Task<ActionResult<FreightviewCredentialDto>> CreateFreightview([FromBody] SaveFreightviewCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("freightview/{id:guid}")]
    public async Task<ActionResult<FreightviewCredentialDto>> UpdateFreightview(Guid id, [FromBody] SaveFreightviewCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("freightview/{id:guid}")]
    public async Task<IActionResult> DeleteFreightview(Guid id)
    {
        await Mediator.Send(new DeleteFreightviewCredentialCommand(id));
        return NoContent();
    }
}
