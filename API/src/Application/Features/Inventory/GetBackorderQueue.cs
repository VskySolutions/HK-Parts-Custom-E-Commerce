using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Inventory;

/// <summary>
/// Lists open order lines that are waiting on backordered stock, FIFO by placement (AC-ORD-006.4).
/// A line is "backordered" when its (product/variant, assigned store) inventory is ≤0 available and the
/// product/variant allows backorders. Optional filters: product, store, and an estimated-restock-by date.
/// </summary>
public record GetBackorderQueueQuery(
    Guid? ProductId = null,
    Guid? StoreId = null,
    DateTime? RestockBy = null,
    int Page = 1,
    int PageSize = 50,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<BackorderQueueRowDto>>;

public class GetBackorderQueueQueryHandler : IRequestHandler<GetBackorderQueueQuery, PaginatedList<BackorderQueueRowDto>>
{
    // Backorder rows are assembled in memory (no backing entity/DbSet), so sorting is applied to the
    // in-memory list over the DTO's own properties. The default preserves the FIFO-by-placement order.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["orderNumber"] = nameof(BackorderQueueRowDto.OrderNumber),
        ["productName"] = nameof(BackorderQueueRowDto.ProductName),
        ["sku"] = nameof(BackorderQueueRowDto.Sku),
        ["quantity"] = nameof(BackorderQueueRowDto.Quantity),
        ["placedOnUtc"] = nameof(BackorderQueueRowDto.PlacedOnUtc),
        ["estimatedRestockDate"] = nameof(BackorderQueueRowDto.EstimatedRestockDate),
    };

    private readonly IApplicationDbContext _db;

    public GetBackorderQueueQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<BackorderQueueRowDto>> Handle(GetBackorderQueueQuery request, CancellationToken cancellationToken)
    {
        var rows = await BackorderQueueBuilder.BuildAsync(_db, request.ProductId, request.StoreId, request.RestockBy, cancellationToken);

        // Sort the in-memory rows; with no SortBy this keeps the existing FIFO-by-placement order.
        var sorted = rows.AsQueryable()
            .ApplySort(request.SortBy, request.SortDescending, SortMap,
                defaultProperty: nameof(BackorderQueueRowDto.PlacedOnUtc), defaultDescending: false)
            .ToList();

        var page = request.Page < 1 ? 1 : request.Page;
        var size = request.PageSize < 1 ? 50 : request.PageSize;
        var items = sorted.Skip((page - 1) * size).Take(size).ToList();
        return new PaginatedList<BackorderQueueRowDto>(items, sorted.Count, page, size);
    }
}

/// <summary>Exports the (unpaged) backorder queue as CSV (AC-ORD-006.5).</summary>
public record ExportBackorderQueueQuery(
    Guid? ProductId = null,
    Guid? StoreId = null,
    DateTime? RestockBy = null) : IRequest<byte[]>;

public class ExportBackorderQueueQueryHandler : IRequestHandler<ExportBackorderQueueQuery, byte[]>
{
    private readonly IApplicationDbContext _db;

    public ExportBackorderQueueQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportBackorderQueueQuery request, CancellationToken cancellationToken)
    {
        var rows = await BackorderQueueBuilder.BuildAsync(_db, request.ProductId, request.StoreId, request.RestockBy, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("OrderNumber,ProductName,Sku,ProductId,ProductVariantId,StoreId,Quantity,PlacedOnUtc,EstimatedRestockDate");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.OrderNumber)).Append(',')
              .Append(Csv(r.ProductName)).Append(',')
              .Append(Csv(r.Sku)).Append(',')
              .Append(r.ProductId).Append(',')
              .Append(r.ProductVariantId?.ToString() ?? string.Empty).Append(',')
              .Append(r.StoreId).Append(',')
              .Append(r.Quantity.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(r.PlacedOnUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(r.EstimatedRestockDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty)
              .Append('\n');
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        // Quote when the value contains a comma, quote or newline; double embedded quotes.
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

/// <summary>Shared computation of the backorder queue rows (used by the list + CSV export).</summary>
internal static class BackorderQueueBuilder
{
    private static readonly OrderStatus[] OpenStatuses =
        { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Accepted, OrderStatus.Preparing };

    public static async Task<List<BackorderQueueRowDto>> BuildAsync(
        IApplicationDbContext db, Guid? productId, Guid? storeId, DateTime? restockBy, CancellationToken ct)
    {
        var ordersQuery = db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .Where(o => OpenStatuses.Contains(o.Status) && o.AssignedStoreId != null);

        if (storeId is Guid sid)
            ordersQuery = ordersQuery.Where(o => o.AssignedStoreId == sid);

        // FIFO by placement; bounded to keep the in-memory pass cheap.
        var orders = await ordersQuery
            .OrderBy(o => o.PlacedOnUtc)
            .Take(2000)
            .ToListAsync(ct);

        var lines = orders
            .SelectMany(o => o.Lines.Select(l => (Order: o, Line: l)))
            .Where(x => productId == null || x.Line.ProductId == productId.Value)
            .ToList();

        if (lines.Count == 0)
            return new List<BackorderQueueRowDto>();

        var productIds = lines.Select(x => x.Line.ProductId).Distinct().ToList();
        var variantIds = lines.Where(x => x.Line.ProductVariantId.HasValue)
            .Select(x => x.Line.ProductVariantId!.Value).Distinct().ToList();
        var storeIds = orders.Select(o => o.AssignedStoreId!.Value).Distinct().ToList();

        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.AllowBackorder, p.EstimatedRestockDate })
            .ToDictionaryAsync(p => p.Id, ct);

        var variants = variantIds.Count == 0
            ? new Dictionary<Guid, (bool AllowBackorder, DateTime? Restock)>()
            : (await db.ProductVariants.AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .Select(v => new { v.Id, v.AllowBackorder, v.EstimatedRestockDate })
                .ToListAsync(ct))
                .ToDictionary(v => v.Id, v => (v.AllowBackorder, Restock: v.EstimatedRestockDate));

        var levels = await db.InventoryLevels.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId) && storeIds.Contains(l.StoreId))
            .Select(l => new { l.ProductId, l.ProductVariantId, l.StoreId, Available = l.StockQuantity - l.ReservedQuantity })
            .ToListAsync(ct);
        var available = levels.ToDictionary(l => (l.ProductId, l.ProductVariantId, l.StoreId), l => l.Available);

        var rows = new List<BackorderQueueRowDto>();
        foreach (var (order, line) in lines)
        {
            var store = order.AssignedStoreId!.Value;
            var stock = available.TryGetValue((line.ProductId, line.ProductVariantId, store), out var a) ? a : 0;
            if (stock > 0)
                continue; // in stock — not on the backorder queue

            bool allows;
            DateTime? restock;
            if (line.ProductVariantId is Guid vid && variants.TryGetValue(vid, out var v))
            {
                allows = v.AllowBackorder;
                restock = v.Restock;
            }
            else if (products.TryGetValue(line.ProductId, out var p))
            {
                allows = p.AllowBackorder;
                restock = p.EstimatedRestockDate;
            }
            else
            {
                continue;
            }

            if (!allows)
                continue;
            if (restockBy is DateTime by && (restock == null || restock > by))
                continue;

            rows.Add(new BackorderQueueRowDto
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                StoreId = store,
                Quantity = line.Quantity,
                PlacedOnUtc = order.PlacedOnUtc,
                EstimatedRestockDate = restock,
            });
        }

        return rows;
    }
}
