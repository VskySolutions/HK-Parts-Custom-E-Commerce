using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Manufacturers;

/// <summary>Soft-deletes a manufacturer (idempotent).</summary>
public record DeleteManufacturerCommand(Guid Id) : IRequest;

public class DeleteManufacturerCommandHandler : IRequestHandler<DeleteManufacturerCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteManufacturerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteManufacturerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Manufacturers
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.Manufacturers.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
