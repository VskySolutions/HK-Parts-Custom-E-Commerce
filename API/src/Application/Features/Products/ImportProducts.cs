using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>A single row-level import error.</summary>
public record ImportRowError(int Row, string Field, string Message);

/// <summary>Outcome of a bulk import: counts, or the failing rows when nothing was written (AC-CAT-009.2).</summary>
public class ImportResultDto
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public List<ImportRowError> Errors { get; set; } = new();
}

/// <summary>
/// Imports products from a CSV file (AC-CAT-009.1). All-or-nothing: every row is validated first; if any
/// row fails, nothing is written and the failing rows/fields are returned (AC-CAT-009.2). Existing products
/// are matched by SKU (update); others are created. Every product must resolve a Tax Category (AC-CAT-001.6).
/// </summary>
public record ImportProductsCommand(byte[] Content) : IRequest<ImportResultDto>;

public class ImportProductsCommandHandler : IRequestHandler<ImportProductsCommand, ImportResultDto>
{
    private readonly IApplicationDbContext _db;

    public ImportProductsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ImportResultDto> Handle(ImportProductsCommand request, CancellationToken cancellationToken)
    {
        var result = new ImportResultDto();
        var records = Csv.Parse(Encoding.UTF8.GetString(request.Content));
        if (records.Count < 2)
        {
            result.Errors.Add(new ImportRowError(0, "file", "The file has no data rows."));
            return result;
        }

        var header = records[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(params string[] names) => header.FindIndex(h => names.Contains(h));

        var iName = Col("name");
        var iType = Col("producttype", "type");
        var iTax = Col("taxcategory", "taxcategoryname", "taxcategoryid");
        if (iName < 0 || iType < 0 || iTax < 0)
        {
            result.Errors.Add(new ImportRowError(1, "header", "Required columns: Name, ProductType, TaxCategory."));
            return result;
        }

        var iSku = Col("sku");
        var iPrice = Col("price");
        var iStock = Col("stockquantity", "stock");
        var iSlug = Col("slug");
        var iPublished = Col("ispublished", "published");
        var iShort = Col("shortdescription");

        // Lookups for validation/resolution.
        var taxByName = await _db.TaxCategories.AsNoTracking()
            .ToDictionaryAsync(t => t.Name.ToLower(), t => t.Id, cancellationToken);
        var taxIds = taxByName.Values.ToHashSet();

        var staged = new List<(int Row, string? Sku, Action<Product> Apply)>();

        for (var r = 1; r < records.Count; r++)
        {
            var row = records[r];
            var rowNumber = r + 1; // 1-based, header is row 1
            string Get(int idx) => idx >= 0 && idx < row.Count ? row[idx].Trim() : string.Empty;

            var name = Get(iName);
            if (string.IsNullOrWhiteSpace(name))
                result.Errors.Add(new ImportRowError(rowNumber, "Name", "Name is required."));

            var typeText = Get(iType);
            if (!Enum.TryParse<ProductType>(typeText, ignoreCase: true, out var productType))
                result.Errors.Add(new ImportRowError(rowNumber, "ProductType", $"'{typeText}' is not a valid product type."));

            var taxText = Get(iTax);
            Guid taxId = Guid.Empty;
            if (Guid.TryParse(taxText, out var parsedTaxId) && taxIds.Contains(parsedTaxId))
                taxId = parsedTaxId;
            else if (taxByName.TryGetValue(taxText.ToLowerInvariant(), out var byName))
                taxId = byName;
            else
                result.Errors.Add(new ImportRowError(rowNumber, "TaxCategory", $"Tax category '{taxText}' was not found."));

            decimal? price = null;
            if (iPrice >= 0 && !string.IsNullOrWhiteSpace(Get(iPrice)))
            {
                if (decimal.TryParse(Get(iPrice), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) price = p;
                else result.Errors.Add(new ImportRowError(rowNumber, "Price", $"'{Get(iPrice)}' is not a valid price."));
            }

            // Stock is now held per store in InventoryLevels, so an imported StockQuantity has no store
            // to apply to and is ignored (set stock on the Inventory page). Still validate the format.
            if (iStock >= 0 && !string.IsNullOrWhiteSpace(Get(iStock))
                && !int.TryParse(Get(iStock), NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                result.Errors.Add(new ImportRowError(rowNumber, "StockQuantity", $"'{Get(iStock)}' is not a valid quantity."));

            var published = iPublished >= 0 && ParseBool(Get(iPublished));
            var sku = iSku >= 0 ? Get(iSku) : null;
            var slug = iSlug >= 0 ? Get(iSlug) : null;
            var shortDesc = iShort >= 0 ? Get(iShort) : null;

            staged.Add((rowNumber, string.IsNullOrWhiteSpace(sku) ? null : sku, product =>
            {
                product.Name = name;
                product.ProductType = productType;
                product.TaxCategoryId = taxId;
                product.Sku = string.IsNullOrWhiteSpace(sku) ? null : sku;
                product.Slug = string.IsNullOrWhiteSpace(slug) ? product.Slug : slug;
                product.ShortDescription = string.IsNullOrWhiteSpace(shortDesc) ? product.ShortDescription : shortDesc;
                product.Price = price;
                product.IsPublished = published;
            }));
        }

        // All-or-nothing (AC-CAT-009.2): abort with the report if any row is invalid.
        if (result.Errors.Count > 0)
            return result;

        // Match existing products by SKU for upsert.
        var skus = staged.Where(s => s.Sku != null).Select(s => s.Sku!).Distinct().ToList();
        var existing = skus.Count == 0
            ? new Dictionary<string, Product>()
            : await _db.Products.Where(p => p.Sku != null && skus.Contains(p.Sku))
                .ToDictionaryAsync(p => p.Sku!, cancellationToken);

        foreach (var item in staged)
        {
            if (item.Sku != null && existing.TryGetValue(item.Sku, out var product))
            {
                item.Apply(product);
                result.Updated++;
            }
            else
            {
                product = new Product();
                item.Apply(product);
                _db.Products.Add(product);
                if (item.Sku != null)
                    existing[item.Sku] = product; // later rows with the same new SKU update it
                result.Created++;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        result.Success = true;
        return result;
    }

    private static bool ParseBool(string value)
        => value.Equals("true", StringComparison.OrdinalIgnoreCase)
           || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
           || value == "1";
}
