using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Branding;

/// <summary>Returns the deployment's singleton branding, or a default when none has been configured.</summary>
public record GetBrandingQuery : IRequest<BrandingDto>;

public class GetBrandingQueryHandler : IRequestHandler<GetBrandingQuery, BrandingDto>
{
    private const string FallbackBrandName = "HK Parts";

    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public GetBrandingQueryHandler(IApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<BrandingDto> Handle(GetBrandingQuery request, CancellationToken cancellationToken)
    {
        var branding = await _db.TenantBrandings
            .AsNoTracking()
            .Include(b => b.LogoMedia)
            .Include(b => b.FaviconMedia)
            .FirstOrDefaultAsync(cancellationToken);

        if (branding is not null)
            return BrandingDto.From(branding);

        var brandName = await _settings.GetValueAsync("brand.name", cancellationToken);
        return new BrandingDto
        {
            BrandName = string.IsNullOrWhiteSpace(brandName) ? FallbackBrandName : brandName,
        };
    }
}
