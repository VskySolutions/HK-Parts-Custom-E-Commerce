using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Discounts;

public record GetDiscountQuery(Guid Id) : IRequest<DiscountDto>;

public class GetDiscountQueryHandler : IRequestHandler<GetDiscountQuery, DiscountDto>
{
    private readonly IApplicationDbContext _db;

    public GetDiscountQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<DiscountDto> Handle(GetDiscountQuery request, CancellationToken cancellationToken)
    {
        var discount = await _db.Discounts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.Id);

        return DiscountDto.From(discount);
    }
}
