using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>
/// Creates or updates the stock level (and optional low-stock threshold) for a product/variant at a
/// store; the admin + Store Manager inventory update path (AC-STR-002.2, AC-CAT-011).
/// </summary>
public record UpsertInventoryLevelCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    Guid StoreId,
    int StockQuantity,
    int? LowStockThreshold) : IRequest<InventoryLevelDto>;

public class UpsertInventoryLevelCommandValidator : AbstractValidator<UpsertInventoryLevelCommand>
{
    public UpsertInventoryLevelCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0).When(x => x.LowStockThreshold.HasValue);
    }
}

public class UpsertInventoryLevelCommandHandler : IRequestHandler<UpsertInventoryLevelCommand, InventoryLevelDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public UpsertInventoryLevelCommandHandler(IApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<InventoryLevelDto> Handle(UpsertInventoryLevelCommand request, CancellationToken cancellationToken)
    {
        await _inventory.SetStockLevelAsync(
            request.ProductId, request.ProductVariantId, request.StoreId,
            request.StockQuantity, request.LowStockThreshold, cancellationToken);

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
