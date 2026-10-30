using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

public record GetSpecificationAttributeQuery(Guid Id) : IRequest<SpecificationAttributeDto>;

public class GetSpecificationAttributeQueryHandler : IRequestHandler<GetSpecificationAttributeQuery, SpecificationAttributeDto>
{
    private readonly IApplicationDbContext _db;

    public GetSpecificationAttributeQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<SpecificationAttributeDto> Handle(GetSpecificationAttributeQuery request, CancellationToken cancellationToken)
    {
        var entity = await _db.SpecificationAttributes
            .AsNoTracking()
            .Include(a => a.Options)
            .FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(SpecificationAttribute), request.Id);

        return SpecificationAttributeDto.From(entity);
    }
}
