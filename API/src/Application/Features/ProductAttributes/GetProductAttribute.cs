using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ProductAttributes;

public record GetProductAttributeQuery(Guid Id) : IRequest<ProductAttributeDto>;

public class GetProductAttributeQueryHandler : IRequestHandler<GetProductAttributeQuery, ProductAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public GetProductAttributeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductAttributeDto> Handle(GetProductAttributeQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.ProductAttributes
            .AsNoTracking()
            .Include(a => a.Values)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductAttribute), request.Id);

        return ProductAttributeDto.From(entity);
    }
}
