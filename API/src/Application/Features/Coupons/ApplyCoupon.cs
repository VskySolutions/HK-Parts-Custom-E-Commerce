using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Outcome of applying a coupon to a cart: the stored code and the discount it unlocks.</summary>
public record ApplyCouponResult(Guid CartId, string AppliedCouponCode, Guid? DiscountId);

/// <summary>
/// Applies a coupon code to a cart (REQ-PRP-002). Validates the code via <see cref="ICouponService"/>
/// and, when valid, stores it on the cart; an invalid code is rejected with a conflict (AC-PRP-002.3).
/// Applying does not consume a redemption — that happens on order completion.
/// </summary>
public record ApplyCouponCommand(Guid CartId, string Code) : IRequest<ApplyCouponResult>;

public class ApplyCouponCommandValidator : AbstractValidator<ApplyCouponCommand>
{
    public ApplyCouponCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
    }
}

public class ApplyCouponCommandHandler : IRequestHandler<ApplyCouponCommand, ApplyCouponResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ICouponService _coupons;

    public ApplyCouponCommandHandler(IApplicationDbContext db, ICouponService coupons)
    {
        _db = db;
        _coupons = coupons;
    }

    public async Task<ApplyCouponResult> Handle(ApplyCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await _db.Carts
            .FirstOrDefaultAsync(c => c.Id == request.CartId, cancellationToken)
            ?? throw new NotFoundException(nameof(Cart), request.CartId);

        var validation = await _coupons.ValidateAsync(request.Code, cancellationToken);
        if (!validation.IsValid)
            throw new ConflictException(validation.Error ?? "The coupon code is invalid.");

        cart.AppliedCouponCode = request.Code.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return new ApplyCouponResult(cart.Id, cart.AppliedCouponCode, validation.DiscountId);
    }
}
