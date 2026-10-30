using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsPageGroups;

/// <summary>Soft-deletes a CMS page group (idempotent). Pages keep their (now dangling) group id and
/// simply drop out of the grouped footer/nav until reassigned.</summary>
public record DeleteCmsPageGroupCommand(Guid Id) : IRequest;

public class DeleteCmsPageGroupCommandHandler : IRequestHandler<DeleteCmsPageGroupCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCmsPageGroupCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCmsPageGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSPageGroups
            .FirstOrDefaultAsync(g => g.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.CMSPageGroups.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
