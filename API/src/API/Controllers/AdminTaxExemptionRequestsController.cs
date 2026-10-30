using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.TaxExemptionRequests;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The admin tax-exemption review queue (REQ-TAX-003): inspect customer-submitted requests and their
/// documents, then approve or reject with an optional note. Approving is the ONLY thing that marks a
/// customer tax-exempt (AC-TAX-003.5).
/// </summary>
[Route("api/admin/tax-exemption-requests")]
[RequireModule(Modules.Customers)]
public class AdminTaxExemptionRequestsController : ApiControllerBase
{
    /// <summary>List requests (newest first), filterable by status and customer/certificate search.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<TaxExemptionRequestDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null, [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListTaxExemptionRequestsQuery(page, pageSize, status, search, sortBy, sortDescending)));

    /// <summary>Get one request with its customer details and document links.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxExemptionRequestDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetTaxExemptionRequestQuery(id)));

    /// <summary>Approve the request: marks the customer tax-exempt and copies the certificate/VAT id (AC-TAX-003.5).</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<TaxExemptionRequestDto>> Approve(Guid id, [FromBody] ReviewNoteRequest? body)
        => Ok(await Mediator.Send(new ResolveTaxExemptionRequestCommand(id, Approve: true, body?.AdminNote)));

    /// <summary>Reject the request: the customer stays taxable and may submit a new one (AC-TAX-003.6).</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<TaxExemptionRequestDto>> Reject(Guid id, [FromBody] ReviewNoteRequest? body)
        => Ok(await Mediator.Send(new ResolveTaxExemptionRequestCommand(id, Approve: false, body?.AdminNote)));

    /// <summary>Optional reviewer note carried by both approve and reject.</summary>
    public record ReviewNoteRequest(string? AdminNote);
}
