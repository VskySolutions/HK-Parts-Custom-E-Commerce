using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Wishlist;

/// <summary>
/// Adds a product (optionally a specific variant) to the current customer's wishlist (AC-CHK-002.1).
/// Idempotent: re-adding the same product/variant is a no-op. Never touches inventory (AC-CHK-002.3).
/// </summary>
public record AddWishlistItemCommand(Guid ProductId, Guid? ProductVariantId = null) : IRequest<WishlistDto>;

public class AddWishlistItemCommandValidator : AbstractValidator<AddWishlistItemCommand>
{
    public AddWishlistItemCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}

public class AddWishlistItemCommandHandler : IRequestHandler<AddWishlistItemCommand, WishlistDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;

    public AddWishlistItemCommandHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups)
    {
        _db = db;
        _current = current;
        _groups = groups;
    }

    public async Task<WishlistDto> Handle(AddWishlistItemCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (!product.IsPublished)
            throw new ConflictException("This product is not available.");

        if (request.ProductVariantId is Guid variantId)
        {
            var variant = await _db.ProductVariants
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == variantId, cancellationToken)
                ?? throw new NotFoundException(nameof(ProductVariant), variantId);

            if (variant.ProductId != product.Id)
                throw new ConflictException("The selected variant does not belong to the specified product.");
        }

        var wishlist = await WishlistResolver.ResolveOrCreateAsync(_db, _current, cancellationToken);

        var alreadySaved = wishlist.Items.Any(
            i => i.ProductId == request.ProductId && i.ProductVariantId == request.ProductVariantId);

        if (!alreadySaved)
        {
            wishlist.Items.Add(new WishlistItem
            {
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await WishlistResolver.BuildDtoAsync(_db, _groups, wishlist, cancellationToken);
    }
}
