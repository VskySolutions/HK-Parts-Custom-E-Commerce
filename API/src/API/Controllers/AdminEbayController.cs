using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Ebay;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// eBay channel: connection/settings, reference data, listings and the inventory sync. Every endpoint is
/// scoped to a store — pass <c>?storeId=</c>, or the primary store is used.
/// </summary>
[Route("api/admin/ebay")]
[RequireModule(Modules.Ebay)]
public class AdminEbayController : ApiControllerBase
{
    // ---- Settings & OAuth ---------------------------------------------------------------------------

    [HttpGet("settings")]
    public async Task<ActionResult<EbaySettingsDto>> GetSettings([FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new GetEbaySettingsQuery(storeId)));

    [HttpPut("settings")]
    public async Task<ActionResult<EbaySettingsDto>> SaveSettings([FromQuery] Guid? storeId, [FromBody] SaveEbaySettingsCommand command)
        => Ok(await Mediator.Send(command with { StoreId = storeId ?? command.StoreId }));

    /// <summary>The eBay consent URL (authorization-code grant). The browser navigates there.</summary>
    [HttpGet("oauth/connect-url")]
    public async Task<IActionResult> ConnectUrl([FromQuery] Guid? storeId)
        => Ok(new { url = await Mediator.Send(new GetEbayConnectUrlQuery(storeId)) });

    /// <summary>
    /// eBay redirects the admin's browser here (the RuName accept URL), so it carries no bearer token — the
    /// signed, single-use <c>state</c> is the CSRF / identity check. Ends with a redirect back to the SPA.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code, [FromQuery] string? state, [FromQuery(Name = "error")] string? error,
        [FromServices] IEbayPublicUrlProvider urls)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return Redirect(urls.AdminSettingsUrl("ebay=declined"));

        try
        {
            var storeId = await Mediator.Send(new CompleteEbayOAuthCommand(code, state));
            return Redirect(urls.AdminSettingsUrl($"ebay=connected&storeId={storeId}"));
        }
        catch (ForbiddenAccessException)
        {
            return Redirect(urls.AdminSettingsUrl("ebay=invalid-state"));
        }
        catch (ServiceUnavailableException ex)
        {
            return Redirect(urls.AdminSettingsUrl($"ebay=error&message={Uri.EscapeDataString(ex.Message)}"));
        }
    }

    [HttpPost("oauth/disconnect")]
    public async Task<IActionResult> Disconnect([FromQuery] Guid? storeId)
    {
        await Mediator.Send(new DisconnectEbayCommand(storeId));
        return NoContent();
    }

    /// <summary>Business policies (payment | fulfillment | return), cached 24h; <c>refresh=true</c> reloads.</summary>
    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<EbayPolicy>>> Policies(
        [FromQuery] EbayPolicyType type, [FromQuery] bool refresh = false, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new GetEbayPoliciesQuery(storeId, type, refresh)));

    [HttpPost("location/sync")]
    public async Task<IActionResult> SyncLocation([FromQuery] Guid? storeId)
        => Ok(new { merchantLocationKey = await Mediator.Send(new SyncEbayLocationCommand(storeId)) });

    // ---- Categories ---------------------------------------------------------------------------------

    [HttpPost("categories/refresh")]
    public async Task<IActionResult> RefreshCategories([FromQuery] Guid? storeId)
    {
        await Mediator.Send(new RefreshEbayCategoriesCommand(storeId));
        return Accepted();
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<EbayCategoryDto>>> Categories([FromQuery] string? parentId, [FromQuery] string? search)
        => Ok(await Mediator.Send(new ListEbayCategoriesQuery(parentId, search)));

    [HttpGet("categories/{id}/aspects")]
    public async Task<ActionResult<IReadOnlyList<EbayAspectDefinition>>> Aspects(string id, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new GetEbayCategoryAspectsQuery(storeId, id)));

    [HttpGet("categories/{id}/conditions")]
    public async Task<ActionResult<List<EbayItemConditionDto>>> Conditions(string id, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new GetEbayCategoryConditionsQuery(storeId, id)));

    [HttpGet("categories/{id}/compatibility")]
    public async Task<ActionResult<EbayCompatibilityInfo>> Compatibility(string id, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new GetEbayCategoryCompatibilityQuery(storeId, id)));

    // ---- Listings -----------------------------------------------------------------------------------

    [HttpGet("products/eligible")]
    public async Task<ActionResult<List<EbayEligibleProductDto>>> EligibleProducts([FromQuery] string? search, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new ListEligibleEbayProductsQuery(storeId, search)));

    [HttpGet("listings")]
    public async Task<ActionResult<PaginatedList<EbayListingListItemDto>>> Listings(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null,
        [FromQuery] EbayListingStatus? status = null, [FromQuery] bool includeEnded = false, [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new ListEbayListingsQuery(storeId, page, pageSize, search, status, includeEnded)));

    public sealed record CreateListingBody(Guid ProductId, string CategoryId);

    [HttpPost("listings")]
    public async Task<ActionResult<EbayListingDto>> CreateListing([FromBody] CreateListingBody body, [FromQuery] Guid? storeId)
    {
        var listing = await Mediator.Send(new CreateEbayListingCommand(storeId, body.ProductId, body.CategoryId));
        return CreatedAtAction(nameof(GetListing), new { id = listing.Id, storeId = listing.StoreId }, listing);
    }

    [HttpGet("listings/{id:guid}")]
    public async Task<ActionResult<EbayListingDto>> GetListing(Guid id, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new GetEbayListingQuery(storeId, id)));

    /// <summary>Save; Active also publishes (first time) or revises on eBay. 422 lists eBay's errors.</summary>
    [HttpPut("listings/{id:guid}")]
    public async Task<ActionResult<EbayListingDto>> SaveListing(Guid id, [FromBody] SaveEbayListingCommand command, [FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(command with { Id = id, StoreId = storeId ?? command.StoreId }));

    [HttpPost("listings/{id:guid}/end")]
    public async Task<IActionResult> EndListing(Guid id, [FromQuery] Guid? storeId)
    {
        await Mediator.Send(new EndEbayListingCommand(storeId, id));
        return NoContent();
    }

    [HttpPost("listings/sync-quantities")]
    public async Task<ActionResult<EbayInventorySyncResult>> SyncQuantities([FromQuery] Guid? storeId)
        => Ok(await Mediator.Send(new SyncEbayQuantitiesCommand(storeId)));
}
