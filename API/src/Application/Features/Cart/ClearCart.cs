using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>
/// Empties the caller's cart and returns it. Idempotent: an empty cart is returned even when none
/// existed. <see cref="SessionId"/> identifies a guest cart.
/// </summary>
public record ClearCartCommand(string? SessionId = null) : IRequest<CartDto>;

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ICommerceModeService _commerce;

    public ClearCartCommandHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups, ICommerceModeService commerce)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _commerce = commerce;
    }

    public async Task<CartDto> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.ResolveOrCreateAsync(_db, _current, request.SessionId, cancellationToken);

        if (cart.Items.Count > 0)
        {
            var toRemove = cart.Items.ToList();
            _db.CartItems.RemoveRange(toRemove);
            cart.Items.Clear();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await CartResolver.BuildDtoAsync(_db, _groups, _commerce, cart, cancellationToken);
    }
}
