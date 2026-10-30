using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

/// <summary>Soft-deletes a shipping zone (idempotent).</summary>
public record DeleteShippingZoneCommand(Guid Id) : IRequest;

public class DeleteShippingZoneCommandHandler : IRequestHandler<DeleteShippingZoneCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteShippingZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteShippingZoneCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ShippingZones
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.ShippingZones.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
