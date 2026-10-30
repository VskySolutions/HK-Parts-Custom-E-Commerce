using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

/// <summary>Soft-deletes a product attribute unless it is assigned to one or more products (AC-CAT-003.4).</summary>
public record DeleteProductAttributeCommand(Guid Id) : IRequest;

public class DeleteProductAttributeCommandHandler : IRequestHandler<DeleteProductAttributeCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteProductAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteProductAttributeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductAttributes
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        var inUse = await _db.ProductAttributeMappings
            .AnyAsync(m => m.ProductAttributeId == request.Id, cancellationToken);
        if (inUse)
            throw new ConflictException("Product attribute is in use on one or more products and cannot be deleted.");

        _db.ProductAttributes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
