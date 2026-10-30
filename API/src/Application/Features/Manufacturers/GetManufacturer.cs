using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Manufacturers;

public record GetManufacturerQuery(Guid Id) : IRequest<ManufacturerDto>;

public class GetManufacturerQueryHandler : IRequestHandler<GetManufacturerQuery, ManufacturerDto>
{
    private readonly IApplicationDbContext _db;

    public GetManufacturerQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ManufacturerDto> Handle(GetManufacturerQuery request, CancellationToken cancellationToken)
    {
        var manufacturer = await _db.Manufacturers
            .AsNoTracking()
            .Include(m => m.LogoMedia)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Manufacturer), request.Id);

        return ManufacturerDto.From(manufacturer);
    }
}
