using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The signed-in customer's returns (docs/order-return-requirements.md section 6): what an order can return,
/// request a return (with photos), track it, reply, enter tracking, cancel.
/// </summary>
[Route("api/customer/rmas")]
[Authorize]
public class CustomerRmaController : ApiControllerBase
{
    private const long RequestLimitBytes = RmaFiles.MaxFilesPerRequest * RmaFiles.MaxBytes + 1_048_576;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>What can be returned from one of the caller's orders.</summary>
    [HttpGet("~/api/customer/orders/{orderId:guid}/returnable")]
    public async Task<ActionResult<ReturnEligibilityDto>> Returnable(Guid orderId)
        => Ok(await Mediator.Send(new GetMyOrderReturnabilityQuery(orderId)));

    /// <summary>
    /// Request a return. Accepts JSON, or multipart with the request JSON in <c>payload</c> and up to 5 images in
    /// <c>photos</c> (needed when a reason requires a photo).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(RequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimitBytes)]
    public async Task<ActionResult<RmaDto>> Submit(CancellationToken ct)
    {
        SubmitRmaCommand? command;
        var streams = new List<Stream>();
        try
        {
            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync(ct);
                command = Parse(form["payload"].ToString());
                var photos = new List<RmaUpload>();
                foreach (var file in form.Files.Where(f => f.Name == "photos" && f.Length > 0))
                {
                    var s = file.OpenReadStream();
                    streams.Add(s);
                    photos.Add(new RmaUpload(file.FileName, file.ContentType, file.Length, s));
                }
                command = command with { Photos = photos };
            }
            else
            {
                command = await JsonSerializer.DeserializeAsync<SubmitRmaCommand>(Request.Body, Json, ct)
                    ?? throw BadPayload();
            }
            return Ok(await Mediator.Send(command, ct));
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    /// <summary>The caller's returns.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<RmaSummaryDto>>> ListMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await Mediator.Send(new ListMyRmasQuery(page, pageSize)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RmaDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetMyRmaQuery(id)));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<RmaDto>> Cancel(Guid id)
        => Ok(await Mediator.Send(new CancelMyRmaCommand(id)));

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<RmaDto>> AddMessage(Guid id, [FromBody] AddMyRmaMessageCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPut("{id:guid}/tracking")]
    public async Task<ActionResult<RmaDto>> SetTracking(Guid id, [FromBody] SetMyRmaTrackingCommand command)
        => Ok(await Mediator.Send(command with { RmaId = id }));

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(RmaFiles.MaxBytes + 1_048_576)]
    [RequestFormLimits(MultipartBodyLengthLimit = RmaFiles.MaxBytes + 1_048_576)]
    public async Task<ActionResult<RmaAttachmentDto>> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return Ok(await Mediator.Send(new AddRmaAttachmentCommand(id, AsCustomer: true,
            new RmaUpload(file.FileName, file.ContentType, file.Length, stream)), ct));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid attachmentId, CancellationToken ct)
        => RmaFileResponse.Send(this, await Mediator.Send(new GetRmaAttachmentQuery(id, attachmentId, AsCustomer: true), ct));

    private static SubmitRmaCommand Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw BadPayload();
        try
        {
            return JsonSerializer.Deserialize<SubmitRmaCommand>(json, Json) ?? throw BadPayload();
        }
        catch (JsonException)
        {
            throw BadPayload();
        }
    }

    private static ValidationException BadPayload()
        => new(new[] { new ValidationFailure("payload", "The return request could not be read.") });
}

/// <summary>Public return-policy data for the storefront form (no staff-only values).</summary>
[Route("api/storefront/returns")]
[AllowAnonymous]
public class StorefrontReturnsController : ApiControllerBase
{
    [HttpGet("config")]
    public async Task<ActionResult<PublicReturnSettingsDto>> Config()
        => Ok(await Mediator.Send(new GetPublicReturnSettingsQuery()));

    [HttpGet("reasons")]
    public async Task<ActionResult<List<RmaReasonDto>>> Reasons()
        => Ok(await Mediator.Send(new ListRmaReasonsQuery(CustomerOnly: true)));
}
