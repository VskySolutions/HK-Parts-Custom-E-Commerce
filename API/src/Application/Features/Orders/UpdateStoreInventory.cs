using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>
/// Store manager sets a stock level for a product/variant at their own store. The store is always
/// taken from the manager's assignment, never the request, so a manager can only affect their own
/// store's inventory (AC-STR-002.2).
/// </summary>
public record UpdateStoreInventoryCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    int Quantity,
    int? LowStockThreshold) : IRequest;

public class UpdateStoreInventoryCommandValidator : AbstractValidator<UpdateStoreInventoryCommand>
{
    public UpdateStoreInventoryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public class UpdateStoreInventoryCommandHandler : IRequestHandler<UpdateStoreInventoryCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ICurrentUserService _current;

    public UpdateStoreInventoryCommandHandler(
        IApplicationDbContext db,
        IInventoryService inventory,
        ICurrentUserService current)
    {
        _db = db;
        _inventory = inventory;
        _current = current;
    }

    public async Task Handle(UpdateStoreInventoryCommand request, CancellationToken cancellationToken)
    {
        var storeId = await ResolveManagerStoreIdAsync(cancellationToken);
        await _inventory.SetStockLevelAsync(
            request.ProductId, request.ProductVariantId, storeId,
            request.Quantity, request.LowStockThreshold, cancellationToken);
    }

    private async Task<Guid> ResolveManagerStoreIdAsync(CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        var storeId = await _db.StoreManagerAssignments
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.StoreId)
            .FirstOrDefaultAsync(cancellationToken);

        if (storeId == Guid.Empty)
            throw new ForbiddenAccessException("You are not assigned to a store.");

        return storeId;
    }
}
