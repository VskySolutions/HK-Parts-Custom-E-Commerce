using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Checkout Settings (requirements section 7): session timeout, low-stock lock, terms checkbox, state notices
/// and blacklist matching. Stored as audited platform settings, so changes apply on the next request.
/// </summary>
[Route("api/admin/checkout/settings")]
[RequireModule(Modules.Settings)]
public class AdminCheckoutSettingsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CheckoutSettingsDto>> Get(CancellationToken ct)
        => Ok(await Mediator.Send(new GetCheckoutSettingsQuery(), ct));

    [HttpPut]
    public async Task<ActionResult<CheckoutSettingsDto>> Update([FromBody] UpdateCheckoutSettingsCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));
}

/// <summary>Customer blacklist (PY-10 / PY-15): blocked names and emails, and the log of refused attempts.</summary>
[Route("api/admin/blacklist")]
[RequireModule(Modules.Customers)]
public class AdminCustomerBlacklistController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CustomerBlacklistEntryDto>>> List(
        [FromQuery] string? search, [FromQuery] CustomerBlacklistStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListCustomerBlacklistQuery(search, status, page, pageSize), ct));

    [HttpPost]
    public async Task<ActionResult<CustomerBlacklistEntryDto>> Create([FromBody] SaveCustomerBlacklistEntryCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command with { Id = null }, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerBlacklistEntryDto>> Update(Guid id, [FromBody] SaveCustomerBlacklistEntryCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command with { Id = id }, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteCustomerBlacklistEntryCommand(id), ct);
        return NoContent();
    }

    [HttpGet("hits")]
    public async Task<ActionResult<PaginatedList<CustomerBlacklistHitDto>>> Hits(
        [FromQuery] Guid? entryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListCustomerBlacklistHitsQuery(entryId, page, pageSize), ct));
}
