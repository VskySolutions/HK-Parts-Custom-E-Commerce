using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Currencies;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public (anonymous) storefront currency surface (WO-26, AC-PRP-003.2): the enabled display
/// currencies a buyer may select, each with the code, symbol and exchange rate needed to render
/// converted prices. Currency configuration and rates are managed on <see cref="TenantCurrenciesController"/>.
/// </summary>
[Route("api/storefront/currencies")]
[AllowAnonymous]
public class StorefrontCurrenciesController : ApiControllerBase
{
    /// <summary>List the enabled display currencies (base currency first), each with symbol and rate.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StorefrontCurrencyDto>>> List()
        => Ok(await Mediator.Send(new GetActiveCurrenciesQuery()));
}
