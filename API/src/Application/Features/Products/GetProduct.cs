using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Returns a single product with its full child graph.</summary>
public record GetProductQuery(Guid Id) : IRequest<ProductDto>;

public class GetProductQueryHandler : IRequestHandler<GetProductQuery, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public GetProductQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.Id);

        return ProductDto.From(product);
    }
}
