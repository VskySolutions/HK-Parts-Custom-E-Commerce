using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Replaces the stores a product is listed in. An empty list lists it in every store.</summary>
public record SetProductStoresCommand(Guid ProductId, List<Guid> StoreIds) : IRequest<ProductDto>;

public class SetProductStoresCommandHandler : IRequestHandler<SetProductStoresCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetProductStoresCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetProductStoresCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var storeIds = request.StoreIds?.Distinct().ToList() ?? new List<Guid>();
        if (storeIds.Count > 0)
        {
            var found = await _db.Stores
                .Where(s => storeIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);
            var missing = storeIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(Store), missing[0]);
        }

        var desired = storeIds.ToHashSet();
        foreach (var row in product.StoreMappings.Where(m => !desired.Contains(m.StoreId)).ToList())
            product.StoreMappings.Remove(row);

        foreach (var storeId in storeIds.Where(id => product.StoreMappings.All(m => m.StoreId != id)))
            product.StoreMappings.Add(new ProductStoreMapping { ProductId = product.Id, StoreId = storeId });

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
