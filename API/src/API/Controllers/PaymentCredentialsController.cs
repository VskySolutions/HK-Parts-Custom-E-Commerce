using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Payment-gateway credentials (Stripe, PayPal, Razorpay, Square, Authorize.Net). Each integration is a
/// <c>Credentials_*</c> table with its own typed fields; the single Active row per integration is the one
/// resolved at checkout. Secret fields are encrypted at rest and returned only on the single-record GET.
/// </summary>
[Route("api/integration-credentials")]
[RequireModule(Modules.Credentials)]
public class PaymentCredentialsController : ApiControllerBase
{
    // ---- Stripe ----
    [HttpGet("stripe")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListStripe()
        => Ok(await Mediator.Send(new ListStripeCredentialsQuery()));

    [HttpGet("stripe/{id:guid}")]
    public async Task<ActionResult<StripeCredentialDto>> GetStripe(Guid id)
        => Ok(await Mediator.Send(new GetStripeCredentialQuery(id)));

    [HttpPost("stripe")]
    public async Task<ActionResult<StripeCredentialDto>> CreateStripe([FromBody] SaveStripeCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("stripe/{id:guid}")]
    public async Task<ActionResult<StripeCredentialDto>> UpdateStripe(Guid id, [FromBody] SaveStripeCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("stripe/{id:guid}")]
    public async Task<IActionResult> DeleteStripe(Guid id)
    {
        await Mediator.Send(new DeleteStripeCredentialCommand(id));
        return NoContent();
    }

    // ---- PayPal ----
    [HttpGet("paypal")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListPayPal()
        => Ok(await Mediator.Send(new ListPayPalCredentialsQuery()));

    [HttpGet("paypal/{id:guid}")]
    public async Task<ActionResult<PayPalCredentialDto>> GetPayPal(Guid id)
        => Ok(await Mediator.Send(new GetPayPalCredentialQuery(id)));

    [HttpPost("paypal")]
    public async Task<ActionResult<PayPalCredentialDto>> CreatePayPal([FromBody] SavePayPalCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("paypal/{id:guid}")]
    public async Task<ActionResult<PayPalCredentialDto>> UpdatePayPal(Guid id, [FromBody] SavePayPalCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("paypal/{id:guid}")]
    public async Task<IActionResult> DeletePayPal(Guid id)
    {
        await Mediator.Send(new DeletePayPalCredentialCommand(id));
        return NoContent();
    }

    // ---- Razorpay ----
    [HttpGet("razorpay")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListRazorpay()
        => Ok(await Mediator.Send(new ListRazorpayCredentialsQuery()));

    [HttpGet("razorpay/{id:guid}")]
    public async Task<ActionResult<RazorpayCredentialDto>> GetRazorpay(Guid id)
        => Ok(await Mediator.Send(new GetRazorpayCredentialQuery(id)));

    [HttpPost("razorpay")]
    public async Task<ActionResult<RazorpayCredentialDto>> CreateRazorpay([FromBody] SaveRazorpayCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("razorpay/{id:guid}")]
    public async Task<ActionResult<RazorpayCredentialDto>> UpdateRazorpay(Guid id, [FromBody] SaveRazorpayCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("razorpay/{id:guid}")]
    public async Task<IActionResult> DeleteRazorpay(Guid id)
    {
        await Mediator.Send(new DeleteRazorpayCredentialCommand(id));
        return NoContent();
    }

    // ---- Square ----
    [HttpGet("square")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListSquare()
        => Ok(await Mediator.Send(new ListSquareCredentialsQuery()));

    [HttpGet("square/{id:guid}")]
    public async Task<ActionResult<SquareCredentialDto>> GetSquare(Guid id)
        => Ok(await Mediator.Send(new GetSquareCredentialQuery(id)));

    [HttpPost("square")]
    public async Task<ActionResult<SquareCredentialDto>> CreateSquare([FromBody] SaveSquareCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("square/{id:guid}")]
    public async Task<ActionResult<SquareCredentialDto>> UpdateSquare(Guid id, [FromBody] SaveSquareCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("square/{id:guid}")]
    public async Task<IActionResult> DeleteSquare(Guid id)
    {
        await Mediator.Send(new DeleteSquareCredentialCommand(id));
        return NoContent();
    }

    // ---- Authorize.Net ----
    [HttpGet("authorizenet")]
    public async Task<ActionResult<IReadOnlyList<IntegrationCredentialListItemDto>>> ListAuthorizeNet()
        => Ok(await Mediator.Send(new ListAuthorizeNetCredentialsQuery()));

    [HttpGet("authorizenet/{id:guid}")]
    public async Task<ActionResult<AuthorizeNetCredentialDto>> GetAuthorizeNet(Guid id)
        => Ok(await Mediator.Send(new GetAuthorizeNetCredentialQuery(id)));

    [HttpPost("authorizenet")]
    public async Task<ActionResult<AuthorizeNetCredentialDto>> CreateAuthorizeNet([FromBody] SaveAuthorizeNetCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("authorizenet/{id:guid}")]
    public async Task<ActionResult<AuthorizeNetCredentialDto>> UpdateAuthorizeNet(Guid id, [FromBody] SaveAuthorizeNetCredentialCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("authorizenet/{id:guid}")]
    public async Task<IActionResult> DeleteAuthorizeNet(Guid id)
    {
        await Mediator.Send(new DeleteAuthorizeNetCredentialCommand(id));
        return NoContent();
    }
}
