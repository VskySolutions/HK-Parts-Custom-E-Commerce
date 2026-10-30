using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Soft-deletes a product collection (idempotent). The collection then drops out of every admin and
/// storefront read via its global query filter; its item rows are left in place and simply never read.</summary>
public record DeleteCmsProductCollectionCommand(Guid Id) : IRequest;

public class DeleteCmsProductCollectionCommandHandler : IRequestHandler<DeleteCmsProductCollectionCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCmsProductCollectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCmsProductCollectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSProductCollections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        // Remove() on a soft-deletable entity is converted to a soft delete by AppDbContext on save.
        _db.CMSProductCollections.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
