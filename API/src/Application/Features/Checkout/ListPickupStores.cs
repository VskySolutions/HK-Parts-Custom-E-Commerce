using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>A store a buyer can collect from, with address + operating hours (AC-SHP-004.1).</summary>
public class PickupStoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? OperatingHoursJson { get; set; }
}

/// <summary>Lists stores that offer pickup-in-store (enabled + pickup-enabled) for checkout (AC-SHP-004.1).</summary>
public record ListPickupStoresQuery : IRequest<List<PickupStoreDto>>;

public class ListPickupStoresQueryHandler : IRequestHandler<ListPickupStoresQuery, List<PickupStoreDto>>
{
    private readonly IApplicationDbContext _db;

    public ListPickupStoresQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<PickupStoreDto>> Handle(ListPickupStoresQuery request, CancellationToken cancellationToken)
    {
        // Platform switch off → offer nothing, whatever the individual stores say. A missing configuration
        // row means the admin has never opened the page, which leaves pickup available (the row defaults on).
        var pickupAllowed = await _db.ShippingProviderConfigurations
            .AsNoTracking()
            .Select(c => (bool?)c.PickupEnabled)
            .FirstOrDefaultAsync(cancellationToken) ?? true;

        if (!pickupAllowed)
            return new List<PickupStoreDto>();

        return await _db.Stores
            .Include(s => s.Address)
            .AsNoTracking()
            .Where(s => s.IsEnabled && s.PickupEnabled)
            .OrderBy(s => s.Name)
            .Select(s => new PickupStoreDto
            {
                Id = s.Id,
                Name = s.Name,
                AddressLine1 = s.AddressLine1,
                AddressLine2 = s.AddressLine2,
                City = s.City,
                StateProvince = s.StateProvince,
                PostalCode = s.PostalCode,
                CountryCode = s.CountryCode,
                OperatingHoursJson = s.OperatingHoursJson,
            })
            .ToListAsync(cancellationToken);
    }
}
