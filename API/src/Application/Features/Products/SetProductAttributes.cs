using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Replaces the attributes assigned to a product for variant generation (AC-CAT-002.1).</summary>
public record SetProductAttributesCommand(Guid ProductId, List<Guid> ProductAttributeIds) : IRequest<ProductDto>;

public class SetProductAttributesCommandHandler : IRequestHandler<SetProductAttributesCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductAttributesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductAttributesCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var attributeIds = request.ProductAttributeIds?.Distinct().ToList() ?? new List<Guid>();
        if (attributeIds.Count > 0)
        {
            var found = await _db.ProductAttributes
                .Where(a => attributeIds.Contains(a.Id))
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            var missing = attributeIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(ProductAttribute), missing[0]);
        }

        var desired = attributeIds.ToHashSet();
        foreach (var row in product.AttributeMappings.Where(m => !desired.Contains(m.ProductAttributeId)).ToList())
            product.AttributeMappings.Remove(row);

        for (var i = 0; i < attributeIds.Count; i++)
        {
            var attributeId = attributeIds[i];
            var row = product.AttributeMappings.FirstOrDefault(m => m.ProductAttributeId == attributeId);
            if (row is null)
                product.AttributeMappings.Add(new ProductAttributeMapping { ProductId = product.Id, ProductAttributeId = attributeId, DisplayOrder = i });
            else
                row.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
