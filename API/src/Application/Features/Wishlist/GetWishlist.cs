using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Wishlist;

/// <summary>Returns the current customer's wishlist, creating an empty one if none exists (AC-CHK-002.2).</summary>
public record GetWishlistQuery : IRequest<WishlistDto>;

public class GetWishlistQueryHandler : IRequestHandler<GetWishlistQuery, WishlistDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;

    public GetWishlistQueryHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups)
    {
        _db = db;
        _current = current;
        _groups = groups;
    }

    public async Task<WishlistDto> Handle(GetWishlistQuery request, CancellationToken cancellationToken)
    {
        var wishlist = await WishlistResolver.ResolveOrCreateAsync(_db, _current, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await WishlistResolver.BuildDtoAsync(_db, _groups, wishlist, cancellationToken);
    }
}
