using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.CustomerAddresses;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>The authenticated customer's address book — saved shipping/billing addresses (REQ-CUS-002).</summary>
[Route("api/customer/addresses")]
[Authorize]
public class CustomerAddressesController : ApiControllerBase
{
    /// <summary>
    /// List the current customer's addresses. Pass <c>grouped=true</c> to receive them grouped by type
    /// (shipping/billing) as an address book for checkout selection.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool grouped = false)
        => grouped
            ? Ok(await Mediator.Send(new GetAddressBookQuery()))
            : Ok(await Mediator.Send(new ListAddressesQuery()));

    /// <summary>Get one of the current customer's addresses by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AddressDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetAddressQuery(id)));

    /// <summary>Add a new address to the current customer's address book.</summary>
    [HttpPost]
    public async Task<ActionResult<AddressDto>> Create([FromBody] CreateAddressCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update one of the current customer's addresses (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AddressDto>> Update(Guid id, [FromBody] UpdateAddressCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Make one of the current customer's addresses the default for its type (flips only the flag).</summary>
    [HttpPut("{id:guid}/default")]
    public async Task<ActionResult<AddressDto>> SetDefault(Guid id)
        => Ok(await Mediator.Send(new SetDefaultAddressCommand(id)));

    /// <summary>Soft-delete one of the current customer's addresses.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteAddressCommand(id));
        return NoContent();
    }
}
