using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Replaces a product's category assignments with exactly the requested set (AC-CAT-004.2).</summary>
public record SetProductCategoriesCommand(Guid ProductId, List<Guid> CategoryIds) : IRequest<ProductDto>;

public class SetProductCategoriesCommandHandler : IRequestHandler<SetProductCategoriesCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductCategoriesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductCategoriesCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var categoryIds = request.CategoryIds?.Distinct().ToList() ?? new List<Guid>();
        if (categoryIds.Count > 0)
        {
            var found = await _db.Categories
                .Where(c => categoryIds.Contains(c.Id))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            var missing = categoryIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(Category), missing[0]);
        }

        var desired = categoryIds.ToHashSet();
        foreach (var row in product.ProductCategories.Where(pc => !desired.Contains(pc.CategoryId)).ToList())
            product.ProductCategories.Remove(row);

        for (var i = 0; i < categoryIds.Count; i++)
        {
            var categoryId = categoryIds[i];
            var row = product.ProductCategories.FirstOrDefault(pc => pc.CategoryId == categoryId);
            if (row is null)
                product.ProductCategories.Add(new ProductCategory { ProductId = product.Id, CategoryId = categoryId, DisplayOrder = i });
            else
                row.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
