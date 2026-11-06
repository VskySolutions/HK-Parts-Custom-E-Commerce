using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.GiftCertificates;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Gift certificate administration (permission <c>GiftCertificates</c>, the old role level 1600). Store-scoped:
/// pass <c>?storeId=</c>, or the primary store is used.
/// </summary>
[Route("api/admin/gift-certificates")]
[RequireModule(Modules.GiftCertificates)]
public class AdminGiftCertificatesController : ApiControllerBase
{
    /// <summary>Paged list (default 25, newest first) with search / status / source / has-balance filters.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<GiftCertificateListItemDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? sort = null, [FromQuery] string? dir = null,
        [FromQuery] string? search = null, [FromQuery] GiftCertificateStatus? status = null,
        [FromQuery] GiftCertificateSource? source = null, [FromQuery] bool hasBalance = false, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new ListGiftCertificatesQuery(storeId, page, pageSize, search, status, source, hasBalance, sort, dir)));

    /// <summary>CSV export of the filtered list.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? search = null, [FromQuery] GiftCertificateStatus? status = null,
        [FromQuery] GiftCertificateSource? source = null, [FromQuery] bool hasBalance = false, [FromQuery] Guid? storeId = null)
    {
        var bytes = await Mediator.Send(new ExportGiftCertificatesQuery(
            new ListGiftCertificatesQuery(storeId, 1, int.MaxValue, search, status, source, hasBalance)));
        return File(bytes, "text/csv", $"gift-certificates-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet("settings")]
    public async Task<ActionResult<GiftCertificateSettingsDto>> GetSettings()
        => Ok(await Mediator.Send(new GetGiftCertificateSettingsQuery()));

    [HttpPut("settings")]
    public async Task<ActionResult<GiftCertificateSettingsDto>> SaveSettings([FromBody] SaveGiftCertificateSettingsCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Detail with the ledger and the orders that used the certificate.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GiftCertificateDto>> Get(Guid id, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new GetGiftCertificateQuery(storeId, id)));

    /// <summary>Create; the response carries the generated code.</summary>
    [HttpPost]
    public async Task<ActionResult<GiftCertificateDto>> Create([FromBody] CreateGiftCertificateCommand command, [FromQuery] Guid? storeId = null)
    {
        var created = await Mediator.Send(command with { StoreId = storeId ?? command.StoreId });
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Update status / expiry / recipient, and optionally the balance (comment required; rowVersion checked).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GiftCertificateDto>> Update(Guid id, [FromBody] UpdateGiftCertificateCommand command, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(command with { Id = id, StoreId = storeId ?? command.StoreId }));

    /// <summary>Soft delete (voids the remaining balance). 409 when an open unpaid order uses it.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid? storeId = null)
    {
        await Mediator.Send(new DeleteGiftCertificateCommand(storeId, id));
        return NoContent();
    }

    [HttpPost("{id:guid}/resend-email")]
    public async Task<IActionResult> ResendEmail(Guid id, [FromQuery] Guid? storeId = null)
    {
        await Mediator.Send(new ResendGiftCertificateEmailCommand(storeId, id));
        return Accepted();
    }
}
