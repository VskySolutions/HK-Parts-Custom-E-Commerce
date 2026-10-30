using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Base controller exposing a lazily-resolved MediatR sender for CQRS dispatch.</summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;

    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
