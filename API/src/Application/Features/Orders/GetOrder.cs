using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>Admin view of a single order including its line items.</summary>
public record GetOrderQuery(Guid Id) : IRequest<OrderDto>;

public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto>
{
    private readonly IApplicationDbContext _db;

    public GetOrderQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<OrderDto> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .Include(o => o.AssignedStore)
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.Id);

        return OrderDto.From(order);
    }
}
