using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>File storage administration: connectivity test, upload, and delete.</summary>
[Route("api/admin/storage")]
[RequireModule(Modules.Storage)]
public class AdminStorageController : ApiControllerBase
{
    private readonly IFileStorage _storage;

    public AdminStorageController(IFileStorage storage) => _storage = storage;

    /// <summary>Write-read-delete probe against the active storage provider.</summary>
    [HttpPost("test-connection")]
    public async Task<ActionResult<ConnectivityTestResult>> TestConnection(CancellationToken cancellationToken)
        => Ok(await _storage.TestConnectionAsync(cancellationToken));

    /// <summary>Upload a file and receive its provider-agnostic asset key and public URL.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<ActionResult<StoredFileReference>> Upload(IFormFile file, [FromForm] string? folder, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty file is required.");

        await using var stream = file.OpenReadStream();
        var reference = await _storage.UploadAsync(new FileUploadRequest
        {
            Content = stream,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FolderPath = folder,
        }, cancellationToken);

        return Ok(reference);
    }

    /// <summary>Delete a stored file by asset key (best-effort).</summary>
    [HttpDelete]
    public async Task<IActionResult> Delete([FromQuery] string assetKey, CancellationToken cancellationToken)
    {
        await _storage.DeleteFileAsync(assetKey, cancellationToken);
        return NoContent();
    }
}
