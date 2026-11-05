using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.PriceRules;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Price rules: automatic catalog discounts by category × customer group, pre-calculated into a flat price
/// table by a background rebuild. The category tree for the form is <c>GET /api/admin/categories/tree</c>.
/// </summary>
[Route("api/admin/price-rules")]
[RequireModule(Modules.Catalog)]
public class AdminPriceRulesController : ApiControllerBase
{
    /// <summary>Paged list (default 25 per page, name ascending), filtered by name, status and store.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<PriceRuleListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sort = null,
        [FromQuery] string? dir = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? storeId = null)
        => Ok(await Mediator.Send(new ListPriceRulesQuery(page, pageSize, search, isActive, storeId, sort, dir)));

    /// <summary>One rule, including its customer group ids, category ids and rowVersion.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PriceRuleDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetPriceRuleQuery(id)));

    /// <summary>Create a rule; its prices are rebuilt in the background.</summary>
    [HttpPost]
    public async Task<ActionResult<PriceRuleDto>> Create([FromBody] CreatePriceRuleCommand command)
    {
        var rule = await Mediator.Send(command);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, rule);
    }

    /// <summary>Update a rule (requires rowVersion; 409 when stale); its prices are rebuilt in the background.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PriceRuleDto>> Update(Guid id, [FromBody] UpdatePriceRuleCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete a rule and its pre-calculated prices (immediately).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeletePriceRuleCommand(id));
        return NoContent();
    }

    /// <summary>Rebuild one rule's prices now.</summary>
    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<PriceRuleDto>> Apply(Guid id)
        => Ok(await Mediator.Send(new ApplyPriceRuleCommand(id)));

    /// <summary>Queue a rebuild of every rule (optionally one store's). 202 with the number queued.</summary>
    [HttpPost("apply-all")]
    public async Task<IActionResult> ApplyAll([FromQuery] Guid? storeId = null)
        => Accepted(new { queued = await Mediator.Send(new ApplyAllPriceRulesCommand(storeId)) });

    /// <summary>Preview an unsaved rule: affected product count and the first 50 calculated prices.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PriceRulePreviewResult>> Preview([FromBody] PreviewPriceRuleCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>A saved rule's calculated prices, optionally for one customer group.</summary>
    [HttpGet("{id:guid}/products")]
    public async Task<ActionResult<PaginatedList<PriceRuleProductPriceDto>>> Products(
        Guid id, [FromQuery] Guid? groupId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 25)
        => Ok(await Mediator.Send(new ListPriceRuleProductsQuery(id, groupId, page, pageSize)));

    /// <summary>Customer groups for the rule form, sorted by name, with "Retail" (guests / no group) first.</summary>
    [HttpGet("customer-groups")]
    public async Task<ActionResult<List<PriceRuleGroupOptionDto>>> CustomerGroups()
        => Ok(await Mediator.Send(new ListPriceRuleGroupOptionsQuery()));
}
