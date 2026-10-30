using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Checkout;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Public storefront pickup-in-store surface (AC-SHP-004.1): the stores a buyer may collect from.</summary>
[Route("api/storefront/pickup-stores")]
[AllowAnonymous]
public class StorefrontPickupController : ApiControllerBase
{
    /// <summary>List stores offering pickup-in-store, with address and operating hours.</summary>
    [HttpGet]
    public async Task<ActionResult<List<PickupStoreDto>>> List()
        => Ok(await Mediator.Send(new ListPickupStoresQuery()));
}
