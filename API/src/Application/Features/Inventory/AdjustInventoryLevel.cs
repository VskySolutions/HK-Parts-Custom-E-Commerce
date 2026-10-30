using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>
/// Applies a relative stock adjustment for a product/variant at a store; creates the level when
/// absent (AC-CAT-011).
/// </summary>
public record AdjustInventoryLevelCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    Guid StoreId,
    int Delta) : IRequest<InventoryLevelDto>;

public class AdjustInventoryLevelCommandValidator : AbstractValidator<AdjustInventoryLevelCommand>
{
    public AdjustInventoryLevelCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.Delta).NotEqual(0);
    }
}

public class AdjustInventoryLevelCommandHandler : IRequestHandler<AdjustInventoryLevelCommand, InventoryLevelDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public AdjustInventoryLevelCommandHandler(IApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<InventoryLevelDto> Handle(AdjustInventoryLevelCommand request, CancellationToken cancellationToken)
    {
        await _inventory.AdjustStockAsync(
            request.ProductId, request.ProductVariantId, request.StoreId, request.Delta, cancellationToken);

        var level = await _db.InventoryLevels
            .AsNoTracking()
            .FirstAsync(
                l => l.ProductId == request.ProductId
                    && l.ProductVariantId == request.ProductVariantId
                    && l.StoreId == request.StoreId,
                cancellationToken);

        return InventoryLevelDto.From(level);
    }
}
