using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>
/// Returns the singleton shipping configuration, creating it on first read with every source enabled so the
/// admin UI always has a row to edit and an upgrade does not silently switch shipping off (REQ-SHP-005).
/// </summary>
public record GetShippingConfigurationQuery : IRequest<ShippingConfigurationDto>;

public class GetShippingConfigurationQueryHandler : IRequestHandler<GetShippingConfigurationQuery, ShippingConfigurationDto>
{
    private readonly IApplicationDbContext _db;

    public GetShippingConfigurationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingConfigurationDto> Handle(GetShippingConfigurationQuery request, CancellationToken cancellationToken)
    {
        // Read untracked: the else-branch tops up missing carrier rows purely to shape the response, and an
        // untracked graph cannot leak those phantom rows into a later SaveChanges.
        var config = await _db.ShippingProviderConfigurations
            .AsNoTracking()
            .Include(c => c.Carriers)
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null)
        {
            config = new ShippingProviderConfiguration();
            ShippingConfigurationBuilder.EnsureAllCarriers(config, enabledByDefault: true);
            _db.ShippingProviderConfigurations.Add(config);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            ShippingConfigurationBuilder.EnsureAllCarriers(config, enabledByDefault: false);
        }

        return await ShippingConfigurationBuilder.ToDtoAsync(_db, config, cancellationToken);
    }
}
