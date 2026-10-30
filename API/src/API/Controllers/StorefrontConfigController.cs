using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Commerce;
using HKPartsEcommerce.Application.Features.Payments;
using HKPartsEcommerce.Application.Features.Recaptcha;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Public storefront configuration endpoints consumed by the client app (WO-106/108).</summary>
[Route("api/storefront/config")]
[AllowAnonymous]
public class StorefrontConfigController : ApiControllerBase
{
    /// <summary>Public commerce config: the tenant's mode (Standard | InquiryOnly) and inquiry copy.</summary>
    [HttpGet("commerce")]
    public async Task<ActionResult<PublicCommerceConfigDto>> Commerce(CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetPublicCommerceConfigQuery(), cancellationToken));

    /// <summary>Public reCAPTCHA config (Site Key, version, per-form flags) — never the Secret Key.</summary>
    [HttpGet("recaptcha")]
    public async Task<ActionResult<PublicRecaptchaConfigDto>> Recaptcha()
        => Ok(await Mediator.Send(new GetPublicRecaptchaConfigQuery()));

    /// <summary>Public Square Web Payments SDK config (Application Id, Location Id, environment) — never secrets.</summary>
    [HttpGet("square")]
    public async Task<ActionResult<PublicSquareConfigDto>> Square()
        => Ok(await Mediator.Send(new GetPublicSquareConfigQuery()));

    /// <summary>Public Authorize.Net Accept.js config (API Login ID, Public Client Key, environment) — never secrets.</summary>
    [HttpGet("authorizenet")]
    public async Task<ActionResult<PublicAuthorizeNetConfigDto>> AuthorizeNet()
        => Ok(await Mediator.Send(new GetPublicAuthorizeNetConfigQuery()));
}
