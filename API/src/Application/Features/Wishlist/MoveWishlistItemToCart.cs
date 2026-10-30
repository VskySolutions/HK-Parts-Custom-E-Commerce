using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Wishlist;

/// <summary>
/// Moves a wishlist item into the current customer's cart and removes it from the wishlist (AC-CHK-002.2).
/// The cart add reuses the Cart feature so availability checks and price snapshotting are shared.
/// </summary>
public record MoveWishlistItemToCartCommand(Guid ItemId, int Quantity = 1) : IRequest<WishlistDto>;

public class MoveWishlistItemToCartCommandHandler : IRequestHandler<MoveWishlistItemToCartCommand, WishlistDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ISender _mediator;

    public MoveWishlistItemToCartCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups, ISender mediator)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _mediator = mediator;
    }

    public async Task<WishlistDto> Handle(MoveWishlistItemToCartCommand request, CancellationToken cancellationToken)
    {
        var wishlist = await WishlistResolver.ResolveExistingAsync(_db, _current, cancellationToken);

        var item = wishlist.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw new NotFoundException(nameof(WishlistItem), request.ItemId);

        // A product with a mandatory custom-input attribute can't be added blind — the wishlist holds no
        // typed value for it. Say so plainly instead of letting the cart's field-level 400 surface here.
        var needsInput = await _db.ProductAttributeMappings
            .AsNoTracking()
            .AnyAsync(m => m.ProductId == item.ProductId
                           && m.ProductAttribute!.DisplayType == ProductAttributeDisplayType.CustomInput
                           && m.ProductAttribute.IsRequired,
                cancellationToken);

        if (needsInput)
            throw new ConflictException(
                "This product needs details filled in before it can be added to the cart. Open the product page to add it.");

        // Delegate to the Cart feature: validates the product/variant is purchasable and snapshots the
        // current price (AC-CHK-001.1). For an authenticated customer the session id is ignored.
        var quantity = request.Quantity < 1 ? 1 : request.Quantity;
        await _mediator.Send(
            new HKPartsEcommerce.Application.Features.Cart.AddItemCommand(item.ProductId, item.ProductVariantId, quantity),
            cancellationToken);

        wishlist.Items.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);

        return await WishlistResolver.BuildDtoAsync(_db, _groups, wishlist, cancellationToken);
    }
}
