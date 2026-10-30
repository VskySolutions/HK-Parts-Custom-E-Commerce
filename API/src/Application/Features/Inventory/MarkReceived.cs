using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>
/// Adds accepted returned units back to stock for a product/variant at a store; the RMA receiving
/// path and the only return flow that increments inventory (AC-CAT-011.6).
/// </summary>
public record MarkReceivedCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    Guid StoreId,
    int QuantityAccepted) : IRequest<InventoryLevelDto>;

public class MarkReceivedCommandValidator : AbstractValidator<MarkReceivedCommand>
{
    public MarkReceivedCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.StoreId).NotEmpty();
        RuleFor(x => x.QuantityAccepted).GreaterThan(0);
    }
}

public class MarkReceivedCommandHandler : IRequestHandler<MarkReceivedCommand, InventoryLevelDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;

    public MarkReceivedCommandHandler(IApplicationDbContext db, IInventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<InventoryLevelDto> Handle(MarkReceivedCommand request, CancellationToken cancellationToken)
    {
        await _inventory.MarkAsReceivedAsync(
            request.ProductId, request.ProductVariantId, request.StoreId, request.QuantityAccepted, cancellationToken);

        var level = await _db.InventoryLevels
            .AsNoTracking()
            .FirstOrDefaultAsync(
                l => l.ProductId == request.ProductId
                    && l.ProductVariantId == request.ProductVariantId
                    && l.StoreId == request.StoreId,
                cancellationToken)
            ?? throw new NotFoundException(nameof(InventoryLevel), request.ProductId);

        return InventoryLevelDto.From(level);
    }
}
