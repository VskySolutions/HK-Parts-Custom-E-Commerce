using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>A low-stock row: a product/variant at a store at or below its low-stock threshold (AC-ADM-002.2).</summary>
public class LowStockRowDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantSku { get; set; }
    public string? StoreName { get; set; }
    public int StockQuantity { get; set; }
    public int LowStockThreshold { get; set; }
}

/// <summary>
/// Lists per-store inventory levels at or below their configured low-stock threshold (AC-ADM-002.2). A
/// threshold of 0 disables alerting for that level and is excluded. Lowest stock first.
/// </summary>
public record GetLowStockReportQuery() : IRequest<IReadOnlyList<LowStockRowDto>>;

public class GetLowStockReportQueryHandler : IRequestHandler<GetLowStockReportQuery, IReadOnlyList<LowStockRowDto>>
{
    private readonly IApplicationDbContext _db;

    public GetLowStockReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<LowStockRowDto>> Handle(GetLowStockReportQuery request, CancellationToken cancellationToken)
    {
        return await _db.InventoryLevels.AsNoTracking()
            .Where(l => l.LowStockThreshold > 0 && l.StockQuantity <= l.LowStockThreshold)
            .OrderBy(l => l.StockQuantity)
            .ThenBy(l => l.Product!.Name)
            .Select(l => new LowStockRowDto
            {
                ProductId = l.ProductId,
                ProductName = l.Product != null ? l.Product.Name : "(unknown product)",
                VariantSku = l.ProductVariant != null ? l.ProductVariant.Sku : null,
                StoreName = l.Store != null ? l.Store.Name : null,
                StockQuantity = l.StockQuantity,
                LowStockThreshold = l.LowStockThreshold,
            })
            .ToListAsync(cancellationToken);
    }
}
