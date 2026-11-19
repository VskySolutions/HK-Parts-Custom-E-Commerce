using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.AddressVerification;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Address Verification settings and the "Test address" box (docs/shipstation-verification-requirements.md §5.2).</summary>
[Route("api/admin/address-verification")]
[RequireModule(Modules.Shipping)]
public class AdminAddressVerificationController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AddressVerificationSettingsDto>> Get(CancellationToken ct)
        => Ok(await Mediator.Send(new GetAddressVerificationSettingsQuery(), ct));

    [HttpPut]
    public async Task<ActionResult<AddressVerificationSettingsDto>> Update(
        [FromBody] UpdateAddressVerificationSettingsCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    /// <summary>AD-61: run a provider against a typed address and show the raw answer (no cache).</summary>
    [HttpPost("test")]
    public async Task<ActionResult<AddressVerificationTestDto>> Test(
        [FromBody] TestAddressVerificationCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));
}

/// <summary>
/// Storefront ship-to verification (§6.1, AV-01): <c>verify</c> after the buyer saves the delivery address,
/// <c>accept</c> for their answer to a suggested correction (AV-13). Public like the rest of checkout.
/// </summary>
[Route("api/checkout/address")]
[AllowAnonymous]
public class CheckoutAddressController : ApiControllerBase
{
    [HttpPost("verify")]
    public async Task<ActionResult<CheckoutAddressVerificationDto>> Verify(
        [FromBody] VerifyCheckoutAddressCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    [HttpPost("accept")]
    public async Task<ActionResult<CheckoutAddressVerificationDto>> Accept(
        [FromBody] AcceptCheckoutAddressCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));
}
