using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>
/// Generates the missing product variants for the cartesian product of the product's assigned
/// attribute values (REQ-CAT-002). Existing variants are preserved; only combinations not already
/// represented are created.
/// </summary>
public record GenerateVariantsCommand(Guid ProductId) : IRequest<ProductDto>;

public class GenerateVariantsCommandHandler : IRequestHandler<GenerateVariantsCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public GenerateVariantsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(GenerateVariantsCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .Include(p => p.AttributeMappings)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var attributeIds = product.AttributeMappings.Select(m => m.ProductAttributeId).ToList();
        if (attributeIds.Count == 0)
            throw new ConflictException("Product has no assigned attributes to generate variants from.");

        var attributes = await _db.ProductAttributes
            .AsNoTracking()
            .Include(a => a.Values)
            .Where(a => attributeIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        // One ordered value-set per assigned attribute; attributes with no values cannot contribute.
        var valueSets = product.AttributeMappings
            .OrderBy(m => m.DisplayOrder)
            .Select(m => attributes.FirstOrDefault(a => a.Id == m.ProductAttributeId))
            .Where(a => a is not null)
            .Select(a => a!.Values.OrderBy(v => v.DisplayOrder).Select(v => v.Id).ToList())
            .Where(values => values.Count > 0)
            .ToList();

        if (valueSets.Count == 0)
            throw new ConflictException("Assigned attributes have no values to generate variants from.");

        // Key existing variants by their sorted attribute-value combination so we skip duplicates.
        var existing = product.Variants
            .Select(v => CombinationKey(v.AttributeValues.Select(av => av.ProductAttributeValueId)))
            .ToHashSet();

        var nextDisplayOrder = product.Variants.Count == 0 ? 0 : product.Variants.Max(v => v.DisplayOrder);

        foreach (var combination in CartesianProduct(valueSets))
        {
            var key = CombinationKey(combination);
            if (!existing.Add(key))
                continue;

            var variant = new ProductVariant
            {
                ProductId = product.Id,
                IsEnabled = true,
                DisplayOrder = ++nextDisplayOrder,
            };
            foreach (var valueId in combination)
                variant.AttributeValues.Add(new ProductVariantAttributeValue { ProductAttributeValueId = valueId });

            product.Variants.Add(variant);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .WithFullGraph()
            .FirstAsync(p => p.Id == product.Id, cancellationToken);
        return ProductDto.From(updated);
    }

    private static string CombinationKey(IEnumerable<Guid> valueIds) =>
        string.Join(",", valueIds.OrderBy(id => id));

    private static IEnumerable<List<Guid>> CartesianProduct(List<List<Guid>> sets)
    {
        IEnumerable<List<Guid>> result = new List<List<Guid>> { new() };
        foreach (var set in sets)
        {
            result = result.SelectMany(
                _ => set,
                (accumulator, value) => new List<Guid>(accumulator) { value });
        }
        return result;
    }
}
