using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.DeliveryZones;

/// <summary>Deletes a delivery zone (idempotent).</summary>
public record DeleteDeliveryZoneCommand(Guid Id) : IRequest;

public class DeleteDeliveryZoneCommandHandler : IRequestHandler<DeleteDeliveryZoneCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteDeliveryZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteDeliveryZoneCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.DeliveryZones
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.DeliveryZones.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
