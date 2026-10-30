using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Newsletter;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) newsletter subscription surface (WO-56). The storefront subscribe form
/// (NewsletterForm.vue) posts here. Single opt-in and idempotent — re-subscribing an existing email is a
/// success, so the form can always treat a 200 as "subscribed".
/// </summary>
[Route("api/newsletter")]
[AllowAnonymous]
public class StorefrontNewsletterController : ApiControllerBase
{
    /// <summary>Subscribe an email address to the newsletter.</summary>
    [HttpPost("subscribe")]
    public async Task<ActionResult<SubscribeResultDto>> Subscribe([FromBody] SubscribeCommand command)
        => Ok(await Mediator.Send(command));
}
