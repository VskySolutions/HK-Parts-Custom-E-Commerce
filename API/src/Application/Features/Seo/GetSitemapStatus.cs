using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Returns the cache-derived sitemap status (last-generated time + entry count).</summary>
public record GetSitemapStatusQuery : IRequest<SitemapStatusDto>;

public class GetSitemapStatusQueryHandler : IRequestHandler<GetSitemapStatusQuery, SitemapStatusDto>
{
    private readonly ISeoService _seo;

    public GetSitemapStatusQueryHandler(ISeoService seo) => _seo = seo;

    public Task<SitemapStatusDto> Handle(GetSitemapStatusQuery request, CancellationToken cancellationToken)
    {
        var status = _seo.GetSitemapStatus();
        return Task.FromResult(new SitemapStatusDto
        {
            GeneratedOnUtc = status.GeneratedOnUtc,
            EntryCount = status.EntryCount,
            IsCached = status.IsCached,
        });
    }
}
