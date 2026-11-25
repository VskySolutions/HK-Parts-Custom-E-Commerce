using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Application.Features.Rma;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Guest order access without signing in (docs/guest-checkout-requirements.md §3.7, §3.8). The signed token
/// travels in the query string or body, never as a path segment, and no email is ever put in a URL (N-05).
/// Shares the <c>api/orders</c> prefix with <see cref="OrdersController"/> through distinct action routes.
/// </summary>
[Route("api/orders")]
[AllowAnonymous]
public class GuestOrdersController : ApiControllerBase
{
    private const long ReturnRequestLimitBytes = RmaFiles.MaxFilesPerRequest * RmaFiles.MaxBytes + 1_048_576;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>U-60/U-62: order number + email (or ZIP) → a token for the order page. 404 on any mismatch; 429 after 5 failures in 15 minutes.</summary>
    [HttpPost("lookup")]
    public async Task<ActionResult<GuestOrderLookupResult>> Lookup([FromBody] LookupGuestOrderCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    /// <summary>U-60/U-61: the read-only guest order view, with what the guest may do next (return, cancel).</summary>
    [HttpGet("guest")]
    public async Task<ActionResult<GuestOrderViewDto>> Get([FromQuery] string token, CancellationToken ct)
        => Ok(await Mediator.Send(new GetGuestOrderQuery(token), ct));

    /// <summary>U-70: set a password to create an account from the guest order (a verification email follows).</summary>
    [HttpPost("guest/account")]
    public async Task<ActionResult<CreateAccountFromGuestOrderResult>> CreateAccount(
        [FromBody] CreateAccountFromGuestOrderCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    /// <summary>U-63: the short list of reasons a guest picks from when asking to cancel.</summary>
    [HttpGet("guest/cancel-reasons")]
    public ActionResult<IReadOnlyList<string>> CancelReasons() => Ok(RequestGuestOrderCancelCommandHandler.Reasons);

    /// <summary>U-63: ask to cancel an order that has not shipped (staff decide). Returns the refreshed order view.</summary>
    [HttpPost("guest/cancel")]
    public async Task<ActionResult<GuestOrderViewDto>> RequestCancel([FromBody] RequestGuestOrderCancelCommand command, CancellationToken ct)
        => Ok(await Mediator.Send(command, ct));

    /// <summary>U-63: what can be returned from the guest order (same rules as a signed-in customer).</summary>
    [HttpGet("guest/returnable")]
    public async Task<ActionResult<ReturnEligibilityDto>> Returnable([FromQuery] string token, CancellationToken ct)
        => Ok(await Mediator.Send(new GetGuestOrderReturnabilityQuery(token), ct));

    /// <summary>
    /// U-63: request a return as a guest. Same body as the customer endpoint plus <c>guestToken</c>: JSON, or multipart
    /// with the request JSON in <c>payload</c> and up to 5 images in <c>photos</c>.
    /// </summary>
    [HttpPost("guest/returns")]
    [RequestSizeLimit(ReturnRequestLimitBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ReturnRequestLimitBytes)]
    public async Task<ActionResult<RmaDto>> SubmitReturn(CancellationToken ct)
    {
        var streams = new List<Stream>();
        try
        {
            SubmitRmaCommand command;
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
                command = await JsonSerializer.DeserializeAsync<SubmitRmaCommand>(Request.Body, Json, ct) ?? throw BadPayload();
            }
            if (string.IsNullOrWhiteSpace(command.GuestToken))
                throw new ValidationException(new[] { new ValidationFailure("guestToken", "The order link is missing.") });
            return Ok(await Mediator.Send(command, ct));
        }
        finally
        {
            foreach (var s in streams) await s.DisposeAsync();
        }
    }

    private static SubmitRmaCommand Parse(string json)
    {
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
