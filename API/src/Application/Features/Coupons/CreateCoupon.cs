using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Coupons;

/// <summary>Creates a coupon code bound to a discount (REQ-PRP-002).</summary>
public record CreateCouponCommand(
    string Code,
    Guid DiscountId,
    CouponUsageType UsageType,
    int? MaxRedemptions = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    bool IsActive = true) : IRequest<CouponDto>;

public class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
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

public class CreateCouponCommandHandler : IRequestHandler<CreateCouponCommand, CouponDto>
{
    private readonly IApplicationDbContext _db;

    public CreateCouponCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CouponDto> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();

        if (!await _db.Discounts.AsNoTracking().AnyAsync(d => d.Id == request.DiscountId, cancellationToken))
            throw new NotFoundException(nameof(Discount), request.DiscountId);

        if (await _db.CouponCodes.AsNoTracking().AnyAsync(c => c.Code == code, cancellationToken))
            throw new ConflictException($"A coupon with code '{code}' already exists.");

        var entity = new CouponCode
        {
            Code = code,
            DiscountId = request.DiscountId,
            UsageType = request.UsageType,
            MaxRedemptions = request.UsageType == CouponUsageType.Limited ? request.MaxRedemptions : null,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            IsActive = request.IsActive,
        };

        _db.CouponCodes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return CouponDto.From(entity);
    }
}
