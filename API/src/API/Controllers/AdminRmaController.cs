using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin returns (RMA) management (docs/order-return-requirements.md section 7): the queue, the detail page and
/// every workflow action — review, shipping, receiving, inspection, refund / store credit / exchange, close.
/// </summary>
[Route("api/admin/rma")]
[RequireModule(Modules.Orders)]
public class AdminRmaController : ApiControllerBase
{
    private const long UploadLimitBytes = RmaFiles.MaxBytes + 1_048_576;

    /// <summary>The returns queue (paged, filterable).</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<RmaSummaryDto>>> List(
        [FromQuery] string? status = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, [FromQuery] string? resolution = null,
        [FromQuery] string? sortBy = null, [FromQuery] bool sortDescending = false,
        [FromQuery] Guid? reasonId = null, [FromQuery] bool? requiresFfl = null,
        [FromQuery] DateTime? fromUtc = null, [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? orderId = null, [FromQuery] bool openOnly = false)
        => Ok(await Mediator.Send(new ListRmasQuery(status, page, pageSize, search, resolution, sortBy, sortDescending,
            reasonId, requiresFfl, fromUtc, toUtc, orderId, openOnly)));

    /// <summary>Returns waiting for review (menu badge).</summary>
    [HttpGet("pending-count")]
    public async Task<ActionResult<int>> PendingCount()
        => Ok(await Mediator.Send(new CountPendingRmasQuery()));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RmaDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetRmaQuery(id)));

    /// <summary>What an order can still return (Create return dialog).</summary>
    [HttpGet("~/api/admin/orders/{orderId:guid}/returnable")]
    public async Task<ActionResult<ReturnEligibilityDto>> Returnable(Guid orderId)
        => Ok(await Mediator.Send(new GetOrderReturnabilityQuery(orderId)));

    /// <summary>Order lines, tax and shipping for the one-screen quick return ("Apply credit").</summary>
    [HttpGet("~/api/admin/orders/{orderId:guid}/quick-return")]
    public async Task<ActionResult<QuickReturnFormDto>> QuickReturnForm(Guid orderId)
        => Ok(await Mediator.Send(new GetQuickReturnFormQuery(orderId)));

    /// <summary>Create, receive (restock), inspect and refund a return in one step.</summary>
    [HttpPost("~/api/admin/orders/{orderId:guid}/quick-return")]
    public async Task<ActionResult<QuickReturnResultDto>> ApplyQuickReturn(Guid orderId, [FromBody] ApplyQuickReturnCommand command)
        => Ok(await Mediator.Send(command with { OrderId = orderId }));

    /// <summary>Create a return from the admin (phone / counter request).</summary>
    [HttpPost]
    public async Task<ActionResult<RmaDto>> Create([FromBody] CreateRmaCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<RmaDto>> Approve(Guid id, [FromBody] ApproveRmaCommand? command)
        => Ok(await Mediator.Send((command ?? new ApproveRmaCommand(id)) with { RmaId = id }));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<RmaDto>> Reject(Guid id, [FromBody] RejectRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RmaDto>> Cancel(Guid id, [FromBody] CancelRmaCommand? command)
        => Ok(await Mediator.Send((command ?? new CancelRmaCommand(id)) with { RmaId = id }));

    [HttpPost("{id:guid}/request-info")]
    public async Task<ActionResult<RmaDto>> RequestInfo(Guid id, [FromBody] RequestRmaInfoCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<RmaDto>> AddMessage(Guid id, [FromBody] AddRmaMessageCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPut("{id:guid}/ffl")]
    public async Task<ActionResult<RmaDto>> SetFfl(Guid id, [FromBody] SetRmaFflDetailsCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPut("{id:guid}/lines")]
    public async Task<ActionResult<RmaDto>> UpdateLines(Guid id, [FromBody] UpdateRmaLinesCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPut("{id:guid}/label")]
    public async Task<ActionResult<RmaDto>> SetLabel(Guid id, [FromBody] SetRmaLabelCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/in-transit")]
    public async Task<ActionResult<RmaDto>> MarkInTransit(Guid id, [FromBody] MarkRmaInTransitCommand? command)
        => Ok(await Mediator.Send((command ?? new MarkRmaInTransitCommand(id)) with { RmaId = id }));

    [HttpPost("{id:guid}/receive")]
    public async Task<ActionResult<RmaDto>> Receive(Guid id, [FromBody] ReceiveRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/inspect")]
    public async Task<ActionResult<RmaDto>> Inspect(Guid id, [FromBody] InspectRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    /// <summary>Send the refund (original payment / store credit / record a manual refund).</summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<RmaDto>> Refund(Guid id, [FromBody] RefundRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/exchange")]
    public async Task<ActionResult<RmaDto>> Exchange(Guid id, [FromBody] ExchangeRmaCommand? command)
        => Ok(await Mediator.Send((command ?? new ExchangeRmaCommand(id)) with { RmaId = id }));

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<RmaDto>> Close(Guid id, [FromBody] CloseRmaCommand? command)
        => Ok(await Mediator.Send((command ?? new CloseRmaCommand(id)) with { RmaId = id }));

    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<RmaDto>> Reopen(Guid id, [FromBody] ReopenRmaCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    /// <summary>Attach a file (photo, label PDF) to the return.</summary>
    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(UploadLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimitBytes)]
    public async Task<ActionResult<RmaAttachmentDto>> Upload(Guid id, IFormFile file, [FromForm] Guid? rmaLineItemId, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new AddRmaAttachmentCommand(id, AsCustomer: false,
            new RmaUpload(file.FileName, file.ContentType, file.Length, stream), rmaLineItemId), ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken ct)
        => RmaFileResponse.Send(this, await Mediator.Send(new GetRmaAttachmentQuery(id, attachmentId, AsCustomer: false), ct));
}

/// <summary>Return policy settings and the reasons list (section 15).</summary>
[Route("api/admin/returns")]
[RequireModule(Modules.Orders)]
public class AdminReturnSettingsController : ApiControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<ReturnSettingsDto>> GetSettings()
        => Ok(await Mediator.Send(new GetReturnSettingsQuery()));

    [HttpPut("settings")]
    public async Task<ActionResult<ReturnSettingsDto>> UpdateSettings([FromBody] ReturnSettingsDto settings)
        => Ok(await Mediator.Send(new UpdateReturnSettingsCommand(settings)));

    [HttpGet("reasons")]
    public async Task<ActionResult<List<RmaReasonDto>>> Reasons()
        => Ok(await Mediator.Send(new ListRmaReasonsQuery()));

    [HttpPost("reasons")]
    public async Task<ActionResult<RmaReasonDto>> CreateReason([FromBody] SaveRmaReasonCommand command)
        => Ok(await Mediator.Send(command with { Id = null }));

    [HttpPut("reasons/{id:guid}")]
    public async Task<ActionResult<RmaReasonDto>> UpdateReason(Guid id, [FromBody] SaveRmaReasonCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("reasons/{id:guid}")]
    public async Task<IActionResult> DeleteReason(Guid id)
    {
        await Mediator.Send(new DeleteRmaReasonCommand(id));
        return NoContent();
    }
}

/// <summary>Streams a private RMA file without letting the browser sniff or cache it.</summary>
internal static class RmaFileResponse
{
    public static IActionResult Send(ControllerBase c, RmaFileResult file)
    {
        c.Response.Headers[HeaderNames.CacheControl] = "no-store";
        c.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        var inline = file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || file.ContentType == "application/pdf";
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(file.FileName);
        c.Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        return c.File(file.Content, file.ContentType);
    }
}
