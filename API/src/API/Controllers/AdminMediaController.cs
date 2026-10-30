using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Media;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The unified media upload API (WO-122): a two-step prepare → commit flow plus a searchable media
/// library. Every feature that handles uploaded assets uses these endpoints.
/// </summary>
[Route("api/admin/media")]
[RequireModule(Modules.Storage)]
public class AdminMediaController : ApiControllerBase
{
    /// <summary>Media library: paged, searchable by SEO name / alt text, filterable by media type.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedList<MediaDto>>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null, [FromQuery] string? mediaType = null)
        => Ok(await Mediator.Send(new ListMediaQuery(page, pageSize, search, mediaType)));

    /// <summary>Get a single media item (with its resolved public URL) for the SEO/metadata editor.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MediaDto>> Get(Guid id)
        => Ok(await Mediator.Send(new GetMediaQuery(id)));

    /// <summary>Step 1 — process the upload in memory and return a draft with suggested SEO metadata (no DB write).</summary>
    [HttpPost("prepare")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<MediaDraftDto>> Prepare(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var draft = await Mediator.Send(
            new PrepareMediaCommand(ms.ToArray(), file.FileName, file.ContentType), cancellationToken);
        return Ok(draft);
    }

    /// <summary>Streams the prepared (uncommitted) bytes for preview.</summary>
    [HttpGet("temp/{tempId}")]
    public async Task<IActionResult> Preview(string tempId, CancellationToken cancellationToken)
    {
        var preview = await Mediator.Send(new GetMediaPreviewQuery(tempId), cancellationToken);
        return preview is null ? NotFound() : File(preview.Content, preview.ContentType);
    }

    /// <summary>Step 2 — commit a prepared upload: write the file and create the media record.</summary>
    [HttpPost]
    public async Task<ActionResult<MediaCommitResultDto>> Commit([FromBody] CommitMediaCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Update a media item's SEO / accessibility metadata without re-uploading.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MediaDto>> Update(Guid id, [FromBody] UpdateMediaCommand command)
        => Ok(await Mediator.Send(command with { Id = id }));

    /// <summary>Soft-delete a media item and best-effort remove its file.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteMediaCommand(id));
        return NoContent();
    }
}
