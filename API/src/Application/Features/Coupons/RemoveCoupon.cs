using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Clears any coupon code applied to a cart (REQ-PRP-002).</summary>
public record RemoveCouponCommand(Guid CartId) : IRequest;

public class RemoveCouponCommandValidator : AbstractValidator<RemoveCouponCommand>
{
    public RemoveCouponCommandValidator()
    {
        RuleFor(x => x.CartId).NotEmpty();
    }
}

public class RemoveCouponCommandHandler : IRequestHandler<RemoveCouponCommand>
{
    private readonly IApplicationDbContext _db;

    public RemoveCouponCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RemoveCouponCommand request, CancellationToken cancellationToken)
    {
        var cart = await _db.Carts
            .FirstOrDefaultAsync(c => c.Id == request.CartId, cancellationToken)
            ?? throw new NotFoundException(nameof(Cart), request.CartId);

        cart.AppliedCouponCode = null;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
