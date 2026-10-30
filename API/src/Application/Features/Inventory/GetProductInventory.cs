using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>Returns every stock level held for a product across all stores.</summary>
public record GetProductInventoryQuery(Guid ProductId) : IRequest<IReadOnlyList<InventoryLevelDto>>;

public class GetProductInventoryQueryHandler : IRequestHandler<GetProductInventoryQuery, IReadOnlyList<InventoryLevelDto>>
{
    private readonly IApplicationDbContext _db;

    public GetProductInventoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<InventoryLevelDto>> Handle(GetProductInventoryQuery request, CancellationToken cancellationToken)
    {
        var levels = await _db.InventoryLevels
            .AsNoTracking()
            .Where(l => l.ProductId == request.ProductId)
            .OrderBy(l => l.StoreId)
            .ToListAsync(cancellationToken);

        return levels.Select(InventoryLevelDto.From).ToList();
    }
}
