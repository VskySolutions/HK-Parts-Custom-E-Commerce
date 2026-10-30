using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Returns a page of products ordered by display order then name, with optional filters.</summary>
public record ListProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    ProductType? Type = null,
    bool? IsPublished = null,
    Guid? CategoryId = null,
    Guid? ManufacturerId = null,
    bool? IsFeatured = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<ProductListItemDto>>;

public class ListProductsQueryHandler : IRequestHandler<ListProductsQuery, PaginatedList<ProductListItemDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    // (StockQuantity is a [NotMapped] rollup over InventoryLevels, so it isn't sortable server-side.)
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["sku"] = "Sku",
        ["productType"] = "ProductType",
        ["price"] = "Price",
        ["isPublished"] = "IsPublished",
        ["isFeatured"] = "IsFeatured",
    };

    private readonly IApplicationDbContext _db;

    public ListProductsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ProductListItemDto>> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        // Include per-store inventory so the list DTO can roll up on-hand stock (the StockQuantity column
        // was dropped; stock now lives in InventoryLevels). SplitQuery avoids a cartesian blow-up.
        IQueryable<Product> query = _db.Products.AsNoTracking()
            .Include(p => p.InventoryLevels)
            .Include(p => p.Pictures).ThenInclude(pic => pic.Media)
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Name.Contains(term)
                || (p.Sku != null && p.Sku.Contains(term))
                || (p.Gtin != null && p.Gtin == term));
        }

        if (request.Type.HasValue)
            query = query.Where(p => p.ProductType == request.Type.Value);

        if (request.IsPublished.HasValue)
            query = query.Where(p => p.IsPublished == request.IsPublished.Value);

        if (request.IsFeatured.HasValue)
            query = query.Where(p => p.IsFeatured == request.IsFeatured.Value);

        if (request.ManufacturerId.HasValue)
            query = query.Where(p => p.ManufacturerId == request.ManufacturerId.Value);

        if (request.CategoryId.HasValue)
            query = query.Where(p => p.ProductCategories.Any(c => c.CategoryId == request.CategoryId.Value));

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap);

        var page = await PaginatedList<Product>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ProductListItemDto.From).ToList();
        return new PaginatedList<ProductListItemDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
