using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Stores;

public record GetStoreQuery(Guid Id) : IRequest<StoreDto>;

public class GetStoreQueryHandler : IRequestHandler<GetStoreQuery, StoreDto>
{
    private readonly IApplicationDbContext _db;

    public GetStoreQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<StoreDto> Handle(GetStoreQuery request, CancellationToken cancellationToken)
    {
        var store = await _db.Stores
            .Include(s => s.Address)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Store), request.Id);

        return StoreDto.From(store);
    }
}
