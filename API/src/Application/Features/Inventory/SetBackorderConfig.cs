using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>Enables/disables backorders for a product and sets an optional restock date (AC-CAT-013.1/013.3).</summary>
public record SetProductBackorderCommand(Guid ProductId, bool AllowBackorder, DateTime? EstimatedRestockDate = null)
    : IRequest<BackorderConfigDto>;

public class SetProductBackorderCommandHandler : IRequestHandler<SetProductBackorderCommand, BackorderConfigDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductBackorderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BackorderConfigDto> Handle(SetProductBackorderCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        product.AllowBackorder = request.AllowBackorder;
        // A restock date is only meaningful while backorders are enabled; clearing the flag clears the date.
        product.EstimatedRestockDate = request.AllowBackorder ? request.EstimatedRestockDate : null;

        await _db.SaveChangesAsync(cancellationToken);
        return new BackorderConfigDto { Id = product.Id, AllowBackorder = product.AllowBackorder, EstimatedRestockDate = product.EstimatedRestockDate };
    }
}

/// <summary>Enables/disables backorders for a variant and sets an optional restock date (AC-CAT-013.1/013.3).</summary>
public record SetVariantBackorderCommand(Guid VariantId, bool AllowBackorder, DateTime? EstimatedRestockDate = null)
    : IRequest<BackorderConfigDto>;

public class SetVariantBackorderCommandHandler : IRequestHandler<SetVariantBackorderCommand, BackorderConfigDto>
{
    private readonly IApplicationDbContext _db;

    public SetVariantBackorderCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BackorderConfigDto> Handle(SetVariantBackorderCommand request, CancellationToken cancellationToken)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == request.VariantId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductVariant), request.VariantId);

        variant.AllowBackorder = request.AllowBackorder;
        variant.EstimatedRestockDate = request.AllowBackorder ? request.EstimatedRestockDate : null;

        await _db.SaveChangesAsync(cancellationToken);
        return new BackorderConfigDto { Id = variant.Id, AllowBackorder = variant.AllowBackorder, EstimatedRestockDate = variant.EstimatedRestockDate };
    }
}
