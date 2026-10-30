using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>A single quantity-break input row for <see cref="SetTierPricesCommand"/>.</summary>
public record TierPriceInput(int MinQuantity, decimal Price);

/// <summary>
/// Replaces the tier prices of a product, or of a specific variant when <see cref="ProductVariantId"/>
/// is supplied (REQ-CAT-006).
/// </summary>
public record SetTierPricesCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    List<TierPriceInput> Tiers) : IRequest<ProductDto>;

public class SetTierPricesCommandValidator : AbstractValidator<SetTierPricesCommand>
{
    public SetTierPricesCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleForEach(x => x.Tiers).ChildRules(t =>
        {
            t.RuleFor(i => i.MinQuantity).GreaterThan(0);
            t.RuleFor(i => i.Price).GreaterThanOrEqualTo(0);
        });
    }
}

public class SetTierPricesCommandHandler : IRequestHandler<SetTierPricesCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetTierPricesCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetTierPricesCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var tiers = request.Tiers ?? new List<TierPriceInput>();

        if (request.ProductVariantId is Guid variantId)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == variantId)
                ?? throw new NotFoundException(nameof(ProductVariant), variantId);

            foreach (var existing in variant.TierPrices.ToList())
                variant.TierPrices.Remove(existing);
            foreach (var tier in tiers)
                variant.TierPrices.Add(new TierPrice { MinQuantity = tier.MinQuantity, Price = tier.Price });
        }
        else
        {
            foreach (var existing in product.TierPrices.ToList())
                product.TierPrices.Remove(existing);
            foreach (var tier in tiers)
                product.TierPrices.Add(new TierPrice { ProductId = product.Id, MinQuantity = tier.MinQuantity, Price = tier.Price });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
