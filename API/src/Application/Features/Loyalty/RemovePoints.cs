using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>
/// Clears any staged points redemption from the signed-in customer's active cart (WO-27): zeroes
/// <c>PointsApplied</c> and <c>PointsDiscountAmount</c>. Always allowed (even if the program has since been
/// disabled) so a buyer can undo a redemption.
/// </summary>
public record RemovePointsCommand(string? SessionId = null) : IRequest;

public class RemovePointsCommandHandler : IRequestHandler<RemovePointsCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;

    public RemovePointsCommandHandler(IApplicationDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task Handle(RemovePointsCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required.");

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var cart = await ResolveActiveCartAsync(customer.Id, request.SessionId, cancellationToken);

        cart.PointsApplied = 0;
        cart.PointsDiscountAmount = 0m;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Resolves the customer's non-checked-out cart (customer-keyed first, then session cart), mirroring apply-points.</summary>
    private async Task<HKPartsEcommerce.Domain.Entities.Cart> ResolveActiveCartAsync(Guid customerId, string? sessionId, CancellationToken cancellationToken)
    {
        var cart = await _db.Carts
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsCheckedOut, cancellationToken);
        if (cart is not null)
            return cart;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var sessionCart = await _db.Carts
                .FirstOrDefaultAsync(c => c.SessionId == sessionId.Trim() && c.CustomerId == null && !c.IsCheckedOut, cancellationToken);
            if (sessionCart is not null)
                return sessionCart;
        }

        throw new NotFoundException("No active cart exists for the current user.");
    }
}
