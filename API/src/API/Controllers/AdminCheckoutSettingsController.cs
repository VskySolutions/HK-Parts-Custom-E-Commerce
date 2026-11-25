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

/// <summary>
/// Guest checkout settings (docs/guest-checkout-requirements.md §4.1, A-01..A-05): the on/off switch, guest payment
/// methods, guest order limit, link lifetime and order linking. Audited platform settings (A-06).
/// </summary>
[Route("api/admin/checkout/guest-settings")]
[RequireModule(Modules.Settings)]
public class AdminGuestCheckoutSettingsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GuestCheckoutSettingsDto>> Get(CancellationToken ct)
        => Ok(await Mediator.Send(new GetGuestCheckoutSettingsQuery(), ct));

    [HttpPut]
    public async Task<ActionResult<GuestCheckoutSettingsDto>> Update([FromBody] UpdateGuestCheckoutSettingsCommand command, CancellationToken ct)
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

    /// <summary>Block / unblock history: one row per person with how many times they were blocked and unblocked.</summary>
    [HttpGet("history")]
    public async Task<ActionResult<PaginatedList<CustomerBlacklistHistorySummaryDto>>> History(
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListCustomerBlacklistHistoryQuery(search, page, pageSize), ct));

    /// <summary>Every block / unblock (date, reason, changed by) of one person from the history list.</summary>
    [HttpGet("history/events")]
    public async Task<ActionResult<List<CustomerBlacklistHistoryEventDto>>> HistoryEvents([FromQuery] string subjectKey, CancellationToken ct)
        => Ok(await Mediator.Send(new ListCustomerBlacklistHistoryEventsQuery(subjectKey), ct));

    [HttpGet("hits")]
    public async Task<ActionResult<PaginatedList<CustomerBlacklistHitDto>>> Hits(
        [FromQuery] Guid? entryId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await Mediator.Send(new ListCustomerBlacklistHitsQuery(entryId, page, pageSize), ct));
}
