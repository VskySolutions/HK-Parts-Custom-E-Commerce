using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.BackgroundTasks;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Background task monitoring for the admin dashboard.</summary>
[Route("api/admin/background-tasks")]
[RequireModule(Modules.BackgroundTasks)]
public class AdminBackgroundTasksController : ApiControllerBase
{
    /// <summary>Last execution result and next scheduled run per background task.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BackgroundTaskStatus>>> Get()
        => Ok(await Mediator.Send(new GetBackgroundTasksQuery()));
}
