using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.ContactUsEnquiries;


namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage contact us enquiries.</summary>
[Route("api/admin/contact-us")]
public class ContactUsEnquiriesController : ApiControllerBase
{
    /// <summary>List all contact us enquiries.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<ContactUsDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListContactUsEnquiriesQuery(page, pageSize, search, sortBy, sortDescending)));

    /// <summary>Save contact us enquiries.</summary>
    [HttpPost]
    public async Task<ActionResult<ContactUsDto>> Create([FromBody] CreateContactUsCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a custom field (route id wins over any id in the body).</summary>
    //[HttpPut("{id:guid}")]
    //public async Task<ActionResult<ProductCustomFieldDto>> Update(Guid id, [FromBody] UpdateProductCustomFieldCommand command)
    //    => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a contact us enquiry.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteContactUsEnquiryCommand(id));
        return NoContent();
    }
}
