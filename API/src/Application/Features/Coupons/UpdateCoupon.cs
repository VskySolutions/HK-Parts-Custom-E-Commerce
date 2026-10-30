using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Updates an existing coupon code. The system-managed redemption count is not editable here.</summary>
public record UpdateCouponCommand(
    Guid Id,
    string Code,
    Guid DiscountId,
    CouponUsageType UsageType,
    int? MaxRedemptions = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    bool IsActive = true) : IRequest<CouponDto>;

public class UpdateCouponCommandValidator : AbstractValidator<UpdateCouponCommand>
{
    public UpdateCouponCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Code).NotEmpty().MaximumLength(64);
        RuleFor(x => x.DiscountId).NotEmpty();
        RuleFor(x => x.MaxRedemptions).NotNull().GreaterThan(0)
            .When(x => x.UsageType == CouponUsageType.Limited)
            .WithMessage("MaxRedemptions must be greater than 0 when UsageType is Limited.");
        RuleFor(x => x.EndDateUtc).GreaterThanOrEqualTo(x => x.StartDateUtc!.Value)
            .When(x => x.StartDateUtc.HasValue && x.EndDateUtc.HasValue)
            .WithMessage("EndDateUtc must be on or after StartDateUtc.");
    }
}

public class UpdateCouponCommandHandler : IRequestHandler<UpdateCouponCommand, CouponDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateCouponCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CouponDto> Handle(UpdateCouponCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.CouponCodes
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(CouponCode), request.Id);

        var code = request.Code.Trim();

        if (!await _db.Discounts.AsNoTracking().AnyAsync(d => d.Id == request.DiscountId, cancellationToken))
            throw new NotFoundException(nameof(Discount), request.DiscountId);

        if (await _db.CouponCodes.AsNoTracking().AnyAsync(c => c.Code == code && c.Id != request.Id, cancellationToken))
            throw new ConflictException($"A coupon with code '{code}' already exists.");

        entity.Code = code;
        entity.DiscountId = request.DiscountId;
        entity.UsageType = request.UsageType;
        entity.MaxRedemptions = request.UsageType == CouponUsageType.Limited ? request.MaxRedemptions : null;
        entity.StartDateUtc = request.StartDateUtc;
        entity.EndDateUtc = request.EndDateUtc;
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return CouponDto.From(entity);
    }
}
