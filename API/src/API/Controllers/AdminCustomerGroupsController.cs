using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.CustomerGroups;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Manage customer pricing groups (REQ-CUS-003) — e.g. Wholesale, VIP. Groups control pricing only, never
/// catalog visibility (AC-CUS-003.6), and are distinct from the RBAC roles that grant access.
/// </summary>
[Route("api/admin/customer-groups")]
[RequireModule(Modules.Customers)]
public class AdminCustomerGroupsController : ApiControllerBase
{
    /// <summary>List customer groups; pass activeOnly=true for assignment pickers.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CustomerGroupDto>>> List([FromQuery] bool activeOnly = false)
        => Ok(await Mediator.Send(new ListCustomerGroupsQuery(activeOnly)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerGroupDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCustomerGroupQuery(id)));

    [HttpPost]
    public async Task<ActionResult<CustomerGroupDto>> Create([FromBody] CreateCustomerGroupCommand command)
        => Ok(await Mediator.Send(command));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerGroupDto>> Update(Guid id, [FromBody] UpdateCustomerGroupCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCustomerGroupCommand(id));
        return NoContent();
    }

    /// <summary>Replace the explicit fixed group prices of a group (AC-CUS-003.4).</summary>
    [HttpPut("{id:guid}/group-prices")]
    public async Task<ActionResult<CustomerGroupDto>> SetGroupPrices(Guid id, [FromBody] SetGroupPricesCommand command)
        => Ok(await Mediator.Send(command with { GroupId = id }));
}
