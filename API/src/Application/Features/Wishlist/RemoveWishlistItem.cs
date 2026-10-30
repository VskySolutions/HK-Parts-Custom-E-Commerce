using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Wishlist;

/// <summary>Removes an item from the current customer's wishlist (idempotent) (AC-CHK-002.2).</summary>
public record RemoveWishlistItemCommand(Guid ItemId) : IRequest<WishlistDto>;

public class RemoveWishlistItemCommandHandler : IRequestHandler<RemoveWishlistItemCommand, WishlistDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;

    public RemoveWishlistItemCommandHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups)
    {
        _db = db;
        _current = current;
        _groups = groups;
    }

    public async Task<WishlistDto> Handle(RemoveWishlistItemCommand request, CancellationToken cancellationToken)
    {
        var wishlist = await WishlistResolver.ResolveOrCreateAsync(_db, _current, cancellationToken);

        var item = wishlist.Items.FirstOrDefault(i => i.Id == request.ItemId);
        if (item is not null)
        {
            wishlist.Items.Remove(item);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return await WishlistResolver.BuildDtoAsync(_db, _groups, wishlist, cancellationToken);
    }
}
