using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Soft-deletes a product variant and removes its per-store inventory (idempotent).</summary>
public record DeleteVariantCommand(Guid VariantId) : IRequest;

public class DeleteVariantCommandHandler : IRequestHandler<DeleteVariantCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteVariantCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteVariantCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken);

        if (variant is null)
            return;

        // Remove the variant's per-store inventory levels too. The variant→inventory FK is NoAction
        // (no cascade), so these would otherwise be orphaned and keep counting toward stock, since
        // checkout/order-routing read InventoryLevels (not the catalog rollup).
        var levels = await _db.InventoryLevels
            .Where(i => i.ProductVariantId == request.VariantId)
            .ToListAsync(cancellationToken);
        if (levels.Count > 0)
            _db.InventoryLevels.RemoveRange(levels);

        _db.ProductVariants.Remove(variant);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
