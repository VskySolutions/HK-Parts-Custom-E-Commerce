using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>A single immutable order status transition, projected for the admin audit trail.</summary>
public class OrderStatusHistoryDto
{
    public Guid Id { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public Guid? ChangedById { get; set; }
    public DateTime ChangedOnUtc { get; set; }
    public string? Note { get; set; }

    public static OrderStatusHistoryDto From(OrderStatusHistory h) => new()
    {
        Id = h.Id,
        FromStatus = h.FromStatus.ToString(),
        ToStatus = h.ToStatus.ToString(),
        ChangedById = h.ChangedById,
        ChangedOnUtc = h.ChangedOnUtc,
        Note = h.Note,
    };
}

/// <summary>Admin view of an order's full status history, oldest transition first (AC-ORD-001.3).</summary>
public record GetOrderStatusHistoryQuery(Guid OrderId) : IRequest<List<OrderStatusHistoryDto>>;

public class GetOrderStatusHistoryQueryHandler
    : IRequestHandler<GetOrderStatusHistoryQuery, List<OrderStatusHistoryDto>>
{
    private readonly IApplicationDbContext _db;

    public GetOrderStatusHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<OrderStatusHistoryDto>> Handle(
        GetOrderStatusHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.Orders
            .AsNoTracking()
            .AnyAsync(o => o.Id == request.OrderId, cancellationToken);
        if (!exists)
            throw new NotFoundException(nameof(Order), request.OrderId);

        var history = await _db.OrderStatusHistory
            .AsNoTracking()
            .Where(h => h.OrderId == request.OrderId)
            .OrderBy(h => h.ChangedOnUtc)
            .ToListAsync(cancellationToken);

        return history.Select(OrderStatusHistoryDto.From).ToList();
    }
}
