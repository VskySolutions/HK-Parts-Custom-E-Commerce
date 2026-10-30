using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Stores;

/// <summary>Soft-deletes a store (idempotent).</summary>
public record DeleteStoreCommand(Guid Id) : IRequest;

public class DeleteStoreCommandHandler : IRequestHandler<DeleteStoreCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteStoreCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteStoreCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Stores
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.Stores.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
