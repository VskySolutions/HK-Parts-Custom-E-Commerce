using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.StoreManagers;

/// <summary>Removes a store-manager assignment (idempotent).</summary>
public record RemoveStoreManagerCommand(Guid Id) : IRequest;

public class RemoveStoreManagerCommandHandler : IRequestHandler<RemoveStoreManagerCommand>
{
    private readonly IApplicationDbContext _db;

    public RemoveStoreManagerCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveStoreManagerCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.StoreManagerAssignments
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.StoreManagerAssignments.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
