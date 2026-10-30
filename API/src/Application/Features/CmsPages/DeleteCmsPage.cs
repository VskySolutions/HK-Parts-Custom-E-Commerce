using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsPages;

/// <summary>Soft-deletes a CMS page (idempotent for missing pages). Pre-seeded system pages are protected
/// and cannot be deleted.</summary>
public record DeleteCmsPageCommand(Guid Id) : IRequest;

public class DeleteCmsPageCommandHandler : IRequestHandler<DeleteCmsPageCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCmsPageCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCmsPageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSPages
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        if (entity.IsSystemPage)
            throw new ConflictException("This is a protected system page and cannot be deleted.");

        _db.CMSPages.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
