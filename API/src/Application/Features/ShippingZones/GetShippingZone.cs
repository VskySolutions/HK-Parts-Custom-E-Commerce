using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

public record GetShippingZoneQuery(Guid Id) : IRequest<ShippingZoneDto>;

public class GetShippingZoneQueryHandler : IRequestHandler<GetShippingZoneQuery, ShippingZoneDto>
{
    private readonly IApplicationDbContext _db;

    public GetShippingZoneQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingZoneDto> Handle(GetShippingZoneQuery request, CancellationToken cancellationToken)
    {
        var zone = await _db.ShippingZones
            .AsNoTracking()
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingZone), request.Id);

        return ShippingZoneDto.From(zone);
    }
}
