using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Discounts;

/// <summary>Updates an existing discount rule.</summary>
public record UpdateDiscountCommand(
    Guid Id,
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

public class UpdateDiscountCommandValidator : AbstractValidator<UpdateDiscountCommand>
{
    public UpdateDiscountCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
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

public class UpdateDiscountCommandHandler : IRequestHandler<UpdateDiscountCommand, DiscountDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateDiscountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<DiscountDto> Handle(UpdateDiscountCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Discounts
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Discount), request.Id);

        entity.Name = request.Name;
        entity.Scope = request.Scope;
        entity.Type = request.Type;
        entity.Value = request.Value;
        entity.ProductId = request.ProductId;
        entity.CategoryId = request.CategoryId;
        entity.StartDateUtc = request.StartDateUtc;
        entity.EndDateUtc = request.EndDateUtc;
        entity.MinimumOrderValue = request.MinimumOrderValue;
        entity.IsExclusive = request.IsExclusive;
        entity.IsActive = request.IsActive;
        entity.RequiresCoupon = request.RequiresCoupon;

        await _db.SaveChangesAsync(cancellationToken);
        return DiscountDto.From(entity);
    }
}
