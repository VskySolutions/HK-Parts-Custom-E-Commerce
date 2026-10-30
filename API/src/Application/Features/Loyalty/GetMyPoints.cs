using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>The signed-in customer's own points balance and recent ledger entries (WO-27).</summary>
public record GetMyPointsQuery(int RecentCount = 50) : IRequest<PointsBalanceDto>;

public class GetMyPointsQueryHandler : IRequestHandler<GetMyPointsQuery, PointsBalanceDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public GetMyPointsQueryHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<PointsBalanceDto> Handle(GetMyPointsQuery request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required.");

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var balance = await _db.CustomerPointsBalances.AsNoTracking()
            .Where(b => b.CustomerId == customer.Id)
            .Select(b => (int?)b.Balance)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        var take = request.RecentCount <= 0 ? 50 : Math.Min(request.RecentCount, 200);
        var transactions = await _db.PointsTransactions.AsNoTracking()
            .Where(t => t.CustomerId == customer.Id)
            .OrderByDescending(t => t.CreatedOnUtc)
            .Take(take)
            .Select(t => PointsTransactionDto.From(t))
            .ToListAsync(cancellationToken);

        return new PointsBalanceDto
        {
            CustomerId = customer.Id,
            Balance = balance,
            Transactions = transactions,
        };
    }
}
