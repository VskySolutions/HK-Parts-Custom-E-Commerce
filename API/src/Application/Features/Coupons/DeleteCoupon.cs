using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Soft-deletes a coupon code (idempotent).</summary>
public record DeleteCouponCommand(Guid Id) : IRequest;

public class DeleteCouponCommandHandler : IRequestHandler<DeleteCouponCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteCouponCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CouponCodes
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (entity is null)
            return;

        _db.CouponCodes.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
