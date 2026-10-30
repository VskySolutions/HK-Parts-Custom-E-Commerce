using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Returns the storefront sitemap.xml (memory-cached; regenerated on cache miss).</summary>
public record GetSitemapQuery : IRequest<string>;

public class GetSitemapQueryHandler : IRequestHandler<GetSitemapQuery, string>
{
    private readonly ISeoService _seo;

    public GetSitemapQueryHandler(ISeoService seo) => _seo = seo;

    public Task<string> Handle(GetSitemapQuery request, CancellationToken cancellationToken)
        => _seo.GenerateSitemapXmlAsync(cancellationToken);
}
