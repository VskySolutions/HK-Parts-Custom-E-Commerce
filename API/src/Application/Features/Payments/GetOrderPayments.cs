using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>Lists every payment record for an order, newest first (payment history / admin view).</summary>
public record GetOrderPaymentsQuery(Guid OrderId) : IRequest<IReadOnlyList<PaymentDto>>;

public class GetOrderPaymentsQueryHandler : IRequestHandler<GetOrderPaymentsQuery, IReadOnlyList<PaymentDto>>
{
    private readonly IApplicationDbContext _db;

    public GetOrderPaymentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentDto>> Handle(GetOrderPaymentsQuery request, CancellationToken cancellationToken)
    {
        var payments = await _db.PaymentRecords
            .AsNoTracking()
            .Where(p => p.OrderId == request.OrderId)
            .OrderByDescending(p => p.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        return payments.Select(PaymentDto.From).ToList();
    }
}
