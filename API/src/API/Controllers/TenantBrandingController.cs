using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Branding;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Read and update the deployment's tenant branding, including logo and favicon uploads.</summary>
[Route("api/tenant/branding")]
public class TenantBrandingController : ApiControllerBase
{
    private readonly IFileStorage _storage;

    public TenantBrandingController(IFileStorage storage) => _storage = storage;

    /// <summary>Get the current deployment branding. Public: consumed by the Client App at startup.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<BrandingDto>> Get()
        => Ok(await Mediator.Send(new GetBrandingQuery()));

    /// <summary>Create or update the deployment branding.</summary>
    [HttpPut]
    [RequireModule(Modules.Branding)]
    public async Task<ActionResult<BrandingDto>> Update([FromBody] UpdateBrandingCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Upload an image and set it as the deployment logo.</summary>
    [HttpPost("logo")]
    [RequireModule(Modules.Branding)]
    public Task<ActionResult<BrandingDto>> UploadLogo(IFormFile file, CancellationToken cancellationToken)
        => UploadAssetAsync(file, isLogo: true, cancellationToken);

    /// <summary>Upload an image and set it as the deployment favicon.</summary>
    [HttpPost("favicon")]
    [RequireModule(Modules.Branding)]
    public Task<ActionResult<BrandingDto>> UploadFavicon(IFormFile file, CancellationToken cancellationToken)
        => UploadAssetAsync(file, isLogo: false, cancellationToken);

    private async Task<ActionResult<BrandingDto>> UploadAssetAsync(IFormFile file, bool isLogo, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty image file is required.");

        if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only image files are allowed.");

        if (file.Length > 2_000_000)
            return BadRequest("The image must be 2 MB or smaller.");

        var reference = await _storage.UploadAsync(new FileUploadRequest
        {
            Content = file.OpenReadStream(),
            FileName = file.FileName,
            ContentType = file.ContentType,
            FolderPath = "branding",
        }, cancellationToken);

        var branding = await Mediator.Send(new SetBrandingAssetCommand(isLogo, reference.PublicUrl), cancellationToken);
        return Ok(branding);
    }
}
