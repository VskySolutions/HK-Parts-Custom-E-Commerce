using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Shipments;

/// <summary>Generates a carrier label for a shipment and returns the updated shipment (AC-SHP-002.1/002.2).</summary>
public record GenerateShipmentLabelCommand(Guid ShipmentId) : IRequest<ShipmentDto>;

public class GenerateShipmentLabelCommandHandler : IRequestHandler<GenerateShipmentLabelCommand, ShipmentDto>
{
    private readonly IShippingLabelService _labels;
    private readonly IApplicationDbContext _db;

    public GenerateShipmentLabelCommandHandler(IShippingLabelService labels, IApplicationDbContext db)
    {
        _labels = labels;
        _db = db;
    }

    public async Task<ShipmentDto> Handle(GenerateShipmentLabelCommand request, CancellationToken cancellationToken)
    {
        await _labels.GenerateLabelAsync(request.ShipmentId, cancellationToken);

        var shipment = await _db.Shipments
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.TrackingEvents)
            .FirstAsync(s => s.Id == request.ShipmentId, cancellationToken);
        return ShipmentDto.From(shipment);
    }
}

/// <summary>Latest tracking checkpoints across an order's shipments, newest first (AC-SHP-002.3).</summary>
public record GetOrderTrackingQuery(Guid OrderId) : IRequest<List<ShipmentTrackingDto>>;

public class GetOrderTrackingQueryHandler : IRequestHandler<GetOrderTrackingQuery, List<ShipmentTrackingDto>>
{
    private readonly IApplicationDbContext _db;

    public GetOrderTrackingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ShipmentTrackingDto>> Handle(GetOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        var events = await _db.ShipmentTrackingEvents
            .AsNoTracking()
            .Where(t => _db.Shipments.Any(s => s.Id == t.ShipmentId && s.OrderId == request.OrderId))
            .OrderByDescending(t => t.CheckpointOnUtc)
            .ToListAsync(cancellationToken);
        return events.Select(ShipmentTrackingDto.From).ToList();
    }
}
