using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.Orders;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin download of order documents (WO-47): invoice and packing slip PDFs. Routes are distinct from
/// <see cref="AdminOrdersController"/>, so both controllers share the <c>api/admin/orders</c> prefix.
/// </summary>
[Route("api/admin/orders")]
[RequireModule(Modules.Orders)]
public class AdminOrderDocumentsController : ApiControllerBase
{
    /// <summary>Download the order's invoice as a PDF.</summary>
    [HttpGet("{id:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid id)
    {
        var bytes = await Mediator.Send(new GetInvoicePdfQuery(id));
        return File(bytes, "application/pdf", $"invoice-{id}.pdf");
    }

    /// <summary>Download the order's packing slip as a PDF.</summary>
    [HttpGet("{id:guid}/packing-slip")]
    public async Task<IActionResult> PackingSlip(Guid id)
    {
        var bytes = await Mediator.Send(new GetPackingSlipPdfQuery(id));
        return File(bytes, "application/pdf", $"packing-slip-{id}.pdf");
    }
}
