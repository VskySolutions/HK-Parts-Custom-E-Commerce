using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.DeliveryZones;

/// <summary>Returns the delivery zones for a store, ordered by name.</summary>
public record ListDeliveryZonesQuery(Guid StoreId) : IRequest<IReadOnlyList<DeliveryZoneDto>>;

public class ListDeliveryZonesQueryHandler
    : IRequestHandler<ListDeliveryZonesQuery, IReadOnlyList<DeliveryZoneDto>>
{
    private readonly IApplicationDbContext _db;

    public ListDeliveryZonesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<DeliveryZoneDto>> Handle(ListDeliveryZonesQuery request, CancellationToken cancellationToken)
    {
        var zones = await _db.DeliveryZones
            .AsNoTracking()
            .Where(z => z.StoreId == request.StoreId)
            .OrderBy(z => z.Name)
            .ToListAsync(cancellationToken);

        return zones.Select(DeliveryZoneDto.From).ToList();
    }
}
