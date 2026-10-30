using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Invalidates the cached sitemap and regenerates it, returning the fresh status.</summary>
public record RefreshSitemapCommand : IRequest<SitemapStatusDto>;

public class RefreshSitemapCommandHandler : IRequestHandler<RefreshSitemapCommand, SitemapStatusDto>
{
    private readonly ISeoService _seo;

    public RefreshSitemapCommandHandler(ISeoService seo) => _seo = seo;

    public async Task<SitemapStatusDto> Handle(RefreshSitemapCommand request, CancellationToken cancellationToken)
    {
        // Drop then rebuild so the returned status reflects a freshly-generated sitemap.
        _seo.InvalidateSitemapCache();
        await _seo.GenerateSitemapXmlAsync(cancellationToken);

        var status = _seo.GetSitemapStatus();
        return new SitemapStatusDto
        {
            GeneratedOnUtc = status.GeneratedOnUtc,
            EntryCount = status.EntryCount,
            IsCached = status.IsCached,
        };
    }
}
