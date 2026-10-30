using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.OrderRouting;

/// <summary>
/// Runs the routing engine for a given request and returns the full result (assigned store +
/// evaluation chain). Backs the admin routing-preview endpoint and is reusable by the future
/// checkout orchestrator.
/// </summary>
public record EvaluateRoutingQuery(RoutingRequest Request) : IRequest<RoutingResult>;

public class EvaluateRoutingQueryHandler : IRequestHandler<EvaluateRoutingQuery, RoutingResult>
{
    private readonly IOrderRoutingEngine _engine;

    public EvaluateRoutingQueryHandler(IOrderRoutingEngine engine) => _engine = engine;

    public Task<RoutingResult> Handle(EvaluateRoutingQuery request, CancellationToken cancellationToken)
        => _engine.RouteAsync(request.Request, cancellationToken);
}
