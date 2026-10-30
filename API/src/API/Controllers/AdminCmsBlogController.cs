using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.CmsBlog;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Manage blog/news posts (content, tags, featured image, publish lifecycle).</summary>
[Route("api/admin/blog-posts")]
[RequireModule(Modules.Cms)]
public class AdminCmsBlogController : ApiControllerBase
{
    /// <summary>List blog posts (paged, newest first), optionally filtered by status and a title/slug search.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<CmsBlogPostDto>>> List(
        [FromQuery] CmsContentStatus? status = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
        => Ok(await Mediator.Send(new ListCmsBlogPostsQuery(status, search, page, pageSize, sortBy, sortDescending)));

    /// <summary>Get a single blog post by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CmsBlogPostDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetCmsBlogPostQuery(id)));

    /// <summary>Create a new blog post.</summary>
    [HttpPost]
    public async Task<ActionResult<CmsBlogPostDto>> Create([FromBody] CreateCmsBlogPostCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update an existing blog post (route id wins over any id in the body).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CmsBlogPostDto>> Update(Guid id, [FromBody] UpdateCmsBlogPostCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Publish / unpublish / archive a blog post.</summary>
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<CmsBlogPostDto>> SetStatus(Guid id, [FromBody] SetCmsBlogPostStatusCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Delete (soft) a blog post.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteCmsBlogPostCommand(id));
        return NoContent();
    }
}
