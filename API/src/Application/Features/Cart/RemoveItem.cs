using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Cart;

/// <summary>Removes a single line from the caller's cart. <see cref="SessionId"/> identifies a guest cart.</summary>
public record RemoveItemCommand(Guid ItemId, string? SessionId = null) : IRequest<CartDto>;

public class RemoveItemCommandHandler : IRequestHandler<RemoveItemCommand, CartDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly ICustomerGroupService _groups;
    private readonly ICommerceModeService _commerce;

    public RemoveItemCommandHandler(IApplicationDbContext db, ICurrentUserService current, ICustomerGroupService groups, ICommerceModeService commerce)
    {
        _db = db;
        _current = current;
        _groups = groups;
        _commerce = commerce;
    }

    public async Task<CartDto> Handle(RemoveItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await CartResolver.ResolveExistingAsync(_db, _current, request.SessionId, cancellationToken);

        var item = cart.Items.FirstOrDefault(i => i.Id == request.ItemId)
            ?? throw new NotFoundException(nameof(CartItem), request.ItemId);

        cart.Items.Remove(item);
        _db.CartItems.Remove(item);

        await _db.SaveChangesAsync(cancellationToken);
        return await CartResolver.BuildDtoAsync(_db, _groups, _commerce, cart, cancellationToken);
    }
}
