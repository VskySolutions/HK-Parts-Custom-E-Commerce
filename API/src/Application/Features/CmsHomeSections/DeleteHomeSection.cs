using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsHomeSections;

/// <summary>Soft-deletes a home page section (idempotent — a missing section is treated as already gone).</summary>
public record DeleteHomeSectionCommand(Guid Id) : IRequest;

public class DeleteHomeSectionCommandHandler : IRequestHandler<DeleteHomeSectionCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteHomeSectionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteHomeSectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CMSHomePageSections
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.CMSHomePageSections.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
