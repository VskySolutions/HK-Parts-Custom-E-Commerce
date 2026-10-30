using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Discounts;

/// <summary>Creates a discount rule (REQ-PRP-001).</summary>
public record CreateDiscountCommand(
    string Name,
    DiscountScope Scope,
    DiscountType Type,
    decimal Value,
    Guid? ProductId = null,
    Guid? CategoryId = null,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    decimal? MinimumOrderValue = null,
    bool IsExclusive = false,
    bool IsActive = true,
    bool RequiresCoupon = false) : IRequest<DiscountDto>;

public class CreateDiscountCommandValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Value).LessThanOrEqualTo(100)
            .When(x => x.Type == DiscountType.Percentage)
            .WithMessage("A percentage discount value must be between 0 and 100.");
        RuleFor(x => x.ProductId).NotNull()
            .When(x => x.Scope == DiscountScope.Product)
            .WithMessage("ProductId is required when Scope is Product.");
        RuleFor(x => x.CategoryId).NotNull()
            .When(x => x.Scope == DiscountScope.Category)
            .WithMessage("CategoryId is required when Scope is Category.");
        RuleFor(x => x.MinimumOrderValue).GreaterThanOrEqualTo(0)
            .When(x => x.MinimumOrderValue.HasValue);
        RuleFor(x => x.EndDateUtc).GreaterThanOrEqualTo(x => x.StartDateUtc!.Value)
            .When(x => x.StartDateUtc.HasValue && x.EndDateUtc.HasValue)
            .WithMessage("EndDateUtc must be on or after StartDateUtc.");
    }
}

public class CreateDiscountCommandHandler : IRequestHandler<CreateDiscountCommand, DiscountDto>
{
    private readonly IApplicationDbContext _db;

    public CreateDiscountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<DiscountDto> Handle(CreateDiscountCommand request, CancellationToken cancellationToken)
    {
        var entity = new Discount
        {
            Name = request.Name,
            Scope = request.Scope,
            Type = request.Type,
            Value = request.Value,
            ProductId = request.ProductId,
            CategoryId = request.CategoryId,
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc,
            MinimumOrderValue = request.MinimumOrderValue,
            IsExclusive = request.IsExclusive,
            IsActive = request.IsActive,
            RequiresCoupon = request.RequiresCoupon,
        };

        _db.Discounts.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return DiscountDto.From(entity);
    }
}
