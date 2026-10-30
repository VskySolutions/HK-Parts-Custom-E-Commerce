using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.CmsCategoryConfig;

/// <summary>
/// Removes a category's dynamic page configuration (WO-99). Hard delete — the config is not soft-deletable
/// (one row per category); the pinned children cascade away with it. Idempotent: a missing config is a no-op.
/// </summary>
public record DeleteCategoryPageConfigCommand(Guid CategoryId) : IRequest;

public class DeleteCategoryPageConfigCommandHandler : IRequestHandler<DeleteCategoryPageConfigCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCategoryPageConfigCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCategoryPageConfigCommand request, CancellationToken cancellationToken)
    {
        var config = await _db.CMSCategoryPageConfigs
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, cancellationToken);

        if (config is null)
            return;

        // The DB-level cascade on CMSCategoryPinnedProduct.CategoryPageConfigId removes the pinned children.
        _db.CMSCategoryPageConfigs.Remove(config);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
