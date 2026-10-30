using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

public record GetShippingMethodQuery(Guid Id) : IRequest<ShippingMethodDto>;

public class GetShippingMethodQueryHandler : IRequestHandler<GetShippingMethodQuery, ShippingMethodDto>
{
    private readonly IApplicationDbContext _db;

    public GetShippingMethodQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingMethodDto> Handle(GetShippingMethodQuery request, CancellationToken cancellationToken)
    {
        var method = await _db.ShippingMethods
            .AsNoTracking()
            .Include(m => m.ZoneRates)
                .ThenInclude(r => r.ShippingZone)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingMethod), request.Id);

        return ShippingMethodDto.From(method);
    }
}
