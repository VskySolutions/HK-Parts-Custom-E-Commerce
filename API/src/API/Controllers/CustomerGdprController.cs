using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Features.Gdpr;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// The authenticated customer's GDPR self-service (WO-23): download a portable export of their personal data,
/// and permanently anonymise (erase) their account. The JWT identifies the customer; both actions operate only
/// on the caller's own data.
/// </summary>
[Route("api/customer/gdpr")]
[Authorize]
public class CustomerGdprController : ApiControllerBase
{
    /// <summary>
    /// Generate and download the current customer's personal-data export as a JSON file (right to
    /// portability). Regenerated on demand; a confirmation email is also enqueued.
    /// </summary>
    [HttpPost("export")]
    public async Task<IActionResult> Export()
    {
        var bytes = await Mediator.Send(new RequestDataExportCommand());
        return File(bytes, "application/json", "my-data.json");
    }

    /// <summary>
    /// Permanently erase the current customer's account by anonymising their personal data while preserving
    /// order records (right to erasure). The login is disabled by this call.
    /// </summary>
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount()
    {
        await Mediator.Send(new DeleteMyAccountCommand());
        return NoContent();
    }
}
