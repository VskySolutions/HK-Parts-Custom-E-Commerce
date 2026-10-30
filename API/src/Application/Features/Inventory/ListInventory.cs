using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>Returns a page of inventory levels, optionally filtered by product and/or store.</summary>
public record ListInventoryQuery(Guid? ProductId = null, Guid? StoreId = null, int Page = 1, int PageSize = 20, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<InventoryLevelDto>>;

public class ListInventoryQueryHandler : IRequestHandler<ListInventoryQuery, PaginatedList<InventoryLevelDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["stockQuantity"] = "StockQuantity",
        ["lowStockThreshold"] = "LowStockThreshold",
        ["reservedQuantity"] = "ReservedQuantity",
    };

    private readonly IApplicationDbContext _db;

    public ListInventoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<InventoryLevelDto>> Handle(ListInventoryQuery request, CancellationToken cancellationToken)
    {
        IQueryable<InventoryLevel> query = _db.InventoryLevels.AsNoTracking();

        if (request.ProductId is Guid productId)
            query = query.Where(l => l.ProductId == productId);

        if (request.StoreId is Guid storeId)
            query = query.Where(l => l.StoreId == storeId);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(l => l.ProductId).ThenBy(l => l.StoreId));

        var page = await PaginatedList<InventoryLevel>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(InventoryLevelDto.From).ToList();
        return new PaginatedList<InventoryLevelDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
