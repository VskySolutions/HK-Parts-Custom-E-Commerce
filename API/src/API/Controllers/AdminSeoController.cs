using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Features.Seo;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Admin SEO management (WO-57): robots.txt editor + sitemap status/refresh. Grouped under the CMS module
/// (route/menu key <c>cms.seo</c> on the client); gated by <see cref="Modules.Cms"/>.
/// </summary>
[Route("api/admin/seo")]
[RequireModule(Modules.Cms)]
public class AdminSeoController : ApiControllerBase
{
    /// <summary>Get the current SEO settings (effective robots.txt + whether it is customised).</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<SeoSettingsDto>> GetSettings()
        => Ok(await Mediator.Send(new GetSeoSettingsQuery()));

    /// <summary>Set or clear the custom robots.txt body (a blank body reverts to the default).</summary>
    [HttpPut("robots")]
    public async Task<ActionResult<SeoSettingsDto>> UpdateRobots([FromBody] UpdateRobotsTxtCommand command)
        => Ok(await Mediator.Send(command));

    /// <summary>Cache-derived sitemap status (last-generated time + entry count).</summary>
    [HttpGet("sitemap/status")]
    public async Task<ActionResult<SitemapStatusDto>> SitemapStatus()
        => Ok(await Mediator.Send(new GetSitemapStatusQuery()));

    /// <summary>Invalidate + regenerate the cached sitemap, returning the fresh status.</summary>
    [HttpPost("sitemap/refresh")]
    public async Task<ActionResult<SitemapStatusDto>> RefreshSitemap()
        => Ok(await Mediator.Send(new RefreshSitemapCommand()));
}
