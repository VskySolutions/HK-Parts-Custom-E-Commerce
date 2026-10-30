using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Returns the effective robots.txt body (custom DB-backed value, or the computed default).</summary>
public record GetRobotsTxtQuery : IRequest<string>;

public class GetRobotsTxtQueryHandler : IRequestHandler<GetRobotsTxtQuery, string>
{
    private readonly ISeoService _seo;

    public GetRobotsTxtQueryHandler(ISeoService seo) => _seo = seo;

    public Task<string> Handle(GetRobotsTxtQuery request, CancellationToken cancellationToken)
        => _seo.GetRobotsTxtAsync(cancellationToken);
}
