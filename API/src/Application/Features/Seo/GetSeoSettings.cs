using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>Returns the admin SEO settings: the effective robots.txt and whether it is customised.</summary>
public record GetSeoSettingsQuery : IRequest<SeoSettingsDto>;

public class GetSeoSettingsQueryHandler : IRequestHandler<GetSeoSettingsQuery, SeoSettingsDto>
{
    private readonly ISeoService _seo;
    private readonly ISettingsService _settings;

    public GetSeoSettingsQueryHandler(ISeoService seo, ISettingsService settings)
    {
        _seo = seo;
        _settings = settings;
    }

    public async Task<SeoSettingsDto> Handle(GetSeoSettingsQuery request, CancellationToken cancellationToken)
    {
        var effective = await _seo.GetRobotsTxtAsync(cancellationToken);
        var custom = await _settings.GetValueAsync(SeoSettingKeys.RobotsTxt, cancellationToken);

        return new SeoSettingsDto
        {
            RobotsTxt = effective,
            IsCustomRobotsTxt = !string.IsNullOrWhiteSpace(custom),
        };
    }
}
