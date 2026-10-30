using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>Outcome of applying points to a cart: how many points are staged and the discount they buy (WO-27).</summary>
public record ApplyPointsResult(Guid CartId, int PointsApplied, decimal PointsDiscountAmount, int RemainingBalance);

/// <summary>
/// Stages a loyalty-points redemption on the signed-in customer's active cart (WO-27). Like a coupon,
/// applying does not spend the points — that happens at order placement (the checkout orchestrator calls
/// <see cref="IRewardPointsService.RedeemAsync"/>). Validates the program is enabled, the points do not
/// exceed the balance, and the resulting discount does not exceed the cart subtotal, then records
/// <c>PointsApplied</c> + <c>PointsDiscountAmount</c> on the cart.
/// </summary>
public record ApplyPointsCommand(int Points, string? SessionId = null) : IRequest<ApplyPointsResult>;

public class ApplyPointsCommandValidator : AbstractValidator<ApplyPointsCommand>
{
    public ApplyPointsCommandValidator()
    {
        RuleFor(x => x.Points).GreaterThan(0);
    }
}

public class ApplyPointsCommandHandler : IRequestHandler<ApplyPointsCommand, ApplyPointsResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IRewardPointsService _points;

    public ApplyPointsCommandHandler(IApplicationDbContext db, ICurrentUserService current, IRewardPointsService points)
    {
        _db = db;
        _current = current;
        _points = points;
    }

    public async Task<ApplyPointsResult> Handle(ApplyPointsCommand request, CancellationToken cancellationToken)
    {
        var config = await _points.GetConfigAsync(cancellationToken);
        if (!config.Enabled)
            throw new ConflictException("The loyalty points program is not available right now.");

        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required to redeem points.");

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var cart = await ResolveActiveCartAsync(customer.Id, request.SessionId, cancellationToken);

        // Points must be covered by the customer's balance (redemption itself is deferred to placement).
        var balance = await _points.GetBalanceAsync(customer.Id, cancellationToken);
        if (request.Points > balance)
            throw new ConflictException($"You have {balance} points but tried to redeem {request.Points}.");

        // The discount must not exceed the cart subtotal (checkout also clamps the total at ≥ 0 as a backstop).
        var discount = _points.ToDiscountValue(request.Points, config.RedeemRate);
        var subtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        if (discount > subtotal)
            throw new ConflictException("The points discount cannot exceed the cart subtotal.");

        cart.PointsApplied = request.Points;
        cart.PointsDiscountAmount = discount;
        await _db.SaveChangesAsync(cancellationToken);

        return new ApplyPointsResult(cart.Id, cart.PointsApplied, cart.PointsDiscountAmount, balance);
    }

    /// <summary>
    /// Resolves the signed-in customer's non-checked-out cart, mirroring the checkout orchestrator: their
    /// customer-keyed cart first, then a guest/session cart (storefront carts are session-based even once
    /// signed in) when a session id is supplied.
    /// </summary>
    private async Task<HKPartsEcommerce.Domain.Entities.Cart> ResolveActiveCartAsync(Guid customerId, string? sessionId, CancellationToken cancellationToken)
    {
        var cart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId && !c.IsCheckedOut, cancellationToken);
        if (cart is not null)
            return cart;

        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            var sessionCart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sessionId.Trim() && c.CustomerId == null && !c.IsCheckedOut, cancellationToken);
            if (sessionCart is not null)
                return sessionCart;
        }

        throw new NotFoundException("No active cart exists for the current user.");
    }
}
