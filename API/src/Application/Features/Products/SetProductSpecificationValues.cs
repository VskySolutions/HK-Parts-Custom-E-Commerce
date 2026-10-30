using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Replaces the specification-attribute options assigned to a product (REQ-CAT-003).</summary>
public record SetProductSpecificationValuesCommand(Guid ProductId, List<Guid> SpecificationAttributeOptionIds)
    : IRequest<ProductDto>;

public class SetProductSpecificationValuesCommandHandler
    : IRequestHandler<SetProductSpecificationValuesCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductSpecificationValuesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductSpecificationValuesCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var optionIds = request.SpecificationAttributeOptionIds?.Distinct().ToList() ?? new List<Guid>();
        if (optionIds.Count > 0)
        {
            var found = await _db.SpecificationAttributeOptions
                .Where(o => optionIds.Contains(o.Id))
                .Select(o => o.Id)
                .ToListAsync(cancellationToken);
            var missing = optionIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(SpecificationAttributeOption), missing[0]);
        }

        var desired = optionIds.ToHashSet();
        foreach (var row in product.SpecificationValues.Where(s => !desired.Contains(s.SpecificationAttributeOptionId)).ToList())
            product.SpecificationValues.Remove(row);

        var assigned = product.SpecificationValues.Select(s => s.SpecificationAttributeOptionId).ToHashSet();
        foreach (var optionId in optionIds.Where(id => !assigned.Contains(id)))
            product.SpecificationValues.Add(new ProductSpecificationValue { ProductId = product.Id, SpecificationAttributeOptionId = optionId });

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
