using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.TaxExemptionRequests;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The signed-in customer's tax exemption tab (REQ-TAX-003): check status, upload supporting documents and
/// submit a request for admin review. Plain <c>[Authorize]</c> — storefront customers carry no module claims.
/// </summary>
[Route("api/customer/tax-exemption")]
[Authorize]
public class CustomerTaxExemptionController : ApiControllerBase
{
    /// <summary>Current exemption status and the latest request, if any (AC-TAX-003.3).</summary>
    [HttpGet]
    public async Task<ActionResult<MyTaxExemptionDto>> Get()
        => Ok(await Mediator.Send(new GetMyTaxExemptionQuery()));

    /// <summary>
    /// Upload one supporting tax document, returning its media id to attach to a request (AC-TAX-003.2).
    /// Capped at 5 MB with a PDF/JPG/PNG/WEBP allow-list enforced in the handler.
    /// </summary>
    [HttpPost("documents")]
    [RequestSizeLimit(5_242_880)]
    public async Task<ActionResult<TaxExemptionDocumentUploadDto>> UploadDocument(
        IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var uploaded = await Mediator.Send(
            new UploadTaxExemptionDocumentCommand(ms.ToArray(), file.FileName, file.ContentType), cancellationToken);
        return Ok(uploaded);
    }

    /// <summary>Submit (or re-submit) a tax exemption request for review (AC-TAX-003.1).</summary>
    [HttpPost("request")]
    public async Task<ActionResult<TaxExemptionRequestDto>> Submit([FromBody] SubmitTaxExemptionRequestCommand command)
        => Ok(await Mediator.Send(command));
}
