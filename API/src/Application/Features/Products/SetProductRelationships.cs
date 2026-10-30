using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>
/// Replaces the relationships of a given type (Related / Cross-Sell / Up-Sell) for a product with the
/// requested set of related products; relationships of other types are left untouched (REQ-CAT-007).
/// </summary>
public record SetProductRelationshipsCommand(
    Guid ProductId,
    ProductRelationshipType Type,
    List<Guid> RelatedProductIds) : IRequest<ProductDto>;

public class SetProductRelationshipsCommandHandler : IRequestHandler<SetProductRelationshipsCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductRelationshipsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductRelationshipsCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var relatedIds = request.RelatedProductIds?.Distinct().ToList() ?? new List<Guid>();
        if (relatedIds.Contains(product.Id))
            throw new ConflictException("A product cannot be related to itself.");

        if (relatedIds.Count > 0)
        {
            var found = await _db.Products
                .Where(p => relatedIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            var missing = relatedIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(Product), missing[0]);
        }

        var desired = relatedIds.ToHashSet();
        foreach (var row in product.Relationships
            .Where(r => r.RelationshipType == request.Type && !desired.Contains(r.RelatedProductId))
            .ToList())
        {
            product.Relationships.Remove(row);
        }

        for (var i = 0; i < relatedIds.Count; i++)
        {
            var relatedId = relatedIds[i];
            var row = product.Relationships.FirstOrDefault(r => r.RelationshipType == request.Type && r.RelatedProductId == relatedId);
            if (row is null)
                product.Relationships.Add(new ProductRelationship
                {
                    ProductId = product.Id,
                    RelatedProductId = relatedId,
                    RelationshipType = request.Type,
                    DisplayOrder = i,
                });
            else
                row.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
