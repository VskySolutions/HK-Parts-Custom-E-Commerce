using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Public checkout shipping surface (WO-40). Returns the shipping options available for a prospective
/// shipment — enabled custom methods plus every configured live carrier — with failing carriers
/// silently excluded (AC-SHP-001.3).
/// </summary>
[Route("api/checkout")]
[AllowAnonymous]
public class ShippingController : ApiControllerBase
{
    private readonly IShippingRateService _rateService;

    public ShippingController(IShippingRateService rateService) => _rateService = rateService;

    /// <summary>Quote the available shipping options (custom methods + live carriers) for a shipment.</summary>
    [HttpPost("shipping-rates")]
    public async Task<ActionResult<IReadOnlyList<ShippingRateOption>>> GetShippingRates(
        [FromBody] CarrierRateRequest request, CancellationToken cancellationToken)
        => Ok(await _rateService.GetRatesAsync(request, cancellationToken));
}
