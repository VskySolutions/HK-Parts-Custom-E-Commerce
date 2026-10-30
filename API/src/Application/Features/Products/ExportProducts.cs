using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>An exported file's bytes + content type + suggested filename.</summary>
public record FileExportDto(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Exports the catalog (or a filtered subset) to CSV (AC-CAT-009.3). Filters mirror the product list:
/// type, published state, category. Columns match the import format so an export can be re-imported.
/// </summary>
public record ExportProductsQuery(
    ProductType? Type = null,
    bool? IsPublished = null,
    Guid? CategoryId = null) : IRequest<FileExportDto>;

public class ExportProductsQueryHandler : IRequestHandler<ExportProductsQuery, FileExportDto>
{
    private readonly IApplicationDbContext _db;

    public ExportProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FileExportDto> Handle(ExportProductsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Product> query = _db.Products.AsNoTracking();

        if (request.Type.HasValue)
            query = query.Where(p => p.ProductType == request.Type.Value);
        if (request.IsPublished.HasValue)
            query = query.Where(p => p.IsPublished == request.IsPublished.Value);
        if (request.CategoryId is Guid categoryId)
            query = query.Where(p => p.ProductCategories.Any(c => c.CategoryId == categoryId));

        var products = await query
            .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Name,
                p.Sku,
                p.ProductType,
                p.Price,
                // Stock is per store; export the rolled-up on-hand total across all stores/variants.
                StockQuantity = p.InventoryLevels.Sum(l => (int?)l.StockQuantity) ?? 0,
                p.IsPublished,
                p.Slug,
                p.TaxCategoryId,
            })
            .ToListAsync(cancellationToken);

        var taxNames = await _db.TaxCategories.AsNoTracking()
            .ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);

        var header = new[] { "Name", "Sku", "ProductType", "Price", "StockQuantity", "IsPublished", "Slug", "TaxCategory" };
        var rows = products.Select(p => new string?[]
        {
            p.Name,
            p.Sku,
            p.ProductType.ToString(),
            p.Price?.ToString(CultureInfo.InvariantCulture),
            p.StockQuantity.ToString(CultureInfo.InvariantCulture),
            p.IsPublished ? "true" : "false",
            p.Slug,
            taxNames.TryGetValue(p.TaxCategoryId, out var name) ? name : p.TaxCategoryId.ToString(),
        });

        var csv = Csv.Write(header, rows);
        return new FileExportDto(Encoding.UTF8.GetBytes(csv), "text/csv", "products.csv");
    }
}
