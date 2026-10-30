using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Recaptcha;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Admin reCAPTCHA configuration (WO-106). The Secret Key is write-only and masked on read.</summary>
[Route("api/tenant/recaptcha")]
[RequireModule(Modules.Settings)]
public class RecaptchaController : ApiControllerBase
{
    /// <summary>Get the current reCAPTCHA configuration (Secret Key masked).</summary>
    [HttpGet]
    public async Task<ActionResult<RecaptchaConfigDto>> Get()
        => Ok(await Mediator.Send(new GetRecaptchaConfigQuery()));

    /// <summary>Create or update the reCAPTCHA configuration.</summary>
    [HttpPut]
    public async Task<ActionResult<RecaptchaConfigDto>> Update([FromBody] UpdateRecaptchaConfigCommand command)
        => Ok(await Mediator.Send(command));
}
