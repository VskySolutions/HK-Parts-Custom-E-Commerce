using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Shipments;

/// <summary>Lists the shipments recorded for an order, newest first (AC-ORD-002.5).</summary>
public record ListOrderShipmentsQuery(Guid OrderId) : IRequest<List<ShipmentDto>>;

public class ListOrderShipmentsQueryHandler : IRequestHandler<ListOrderShipmentsQuery, List<ShipmentDto>>
{
    private readonly IApplicationDbContext _db;

    public ListOrderShipmentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ShipmentDto>> Handle(ListOrderShipmentsQuery request, CancellationToken cancellationToken)
    {
        var shipments = await _db.Shipments
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.TrackingEvents)
            .Where(s => s.OrderId == request.OrderId)
            .OrderByDescending(s => s.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return shipments.Select(ShipmentDto.From).ToList();
    }
}
