using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

/// <summary>Soft-deletes a specification attribute unless one of its options is in use on a product (AC-CAT-003.4).</summary>
public record DeleteSpecificationAttributeCommand(Guid Id) : IRequest;

public class DeleteSpecificationAttributeCommandHandler : IRequestHandler<DeleteSpecificationAttributeCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteSpecificationAttributeCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteSpecificationAttributeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.SpecificationAttributes
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        var inUse = await _db.ProductSpecificationValues
            .AnyAsync(v => v.SpecificationAttributeOption!.SpecificationAttributeId == request.Id, cancellationToken);
        if (inUse)
            throw new ConflictException("Specification attribute is in use on one or more products and cannot be deleted.");

        _db.SpecificationAttributes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
