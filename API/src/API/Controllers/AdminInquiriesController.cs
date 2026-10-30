using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Inquiries;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin handling of storefront inquiries (REQ-INQ-001): the lead list, the customer information behind
/// each request, the sales pipeline, sending a quote, and converting an accepted inquiry into an order.
/// </summary>
[Route("api/admin/inquiries")]
[RequireModule(Modules.Inquiries)]
public class AdminInquiriesController : ApiControllerBase
{
    /// <summary>List inquiries (paged, newest first) with server-side status/store/quoted/date filters.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<InquirySummaryDto>>> List(
        [FromQuery] string? status = null,
        [FromQuery] Guid? storeId = null,
        [FromQuery] bool? quoted = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
        => Ok(await Mediator.Send(
            new ListInquiriesQuery(status, storeId, quoted, fromUtc, toUtc, search, page, pageSize, sortBy, sortDescending),
            cancellationToken));

    /// <summary>Get one inquiry: customer information, requested items and pipeline state.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InquiryDto>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(new GetInquiryQuery(id), cancellationToken));

    /// <summary>Move the inquiry along its pipeline and/or save internal notes.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InquiryDto>> Update(
        Guid id, [FromBody] UpdateInquiryCommand command, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>Email the buyer a quote and mark the inquiry as quoted.</summary>
    [HttpPost("{id:guid}/quote")]
    public async Task<ActionResult<InquiryDto>> SendQuote(
        Guid id, [FromBody] SendInquiryQuoteCommand command, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(command with { Id = id }, cancellationToken));

    /// <summary>Convert an accepted inquiry into a payable order (Standard mode only).</summary>
    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<OrderDto>> Convert(
        Guid id, [FromBody] ConvertInquiryToOrderCommand command, CancellationToken cancellationToken)
        => Ok(await Mediator.Send(command with { Id = id }, cancellationToken));
}
