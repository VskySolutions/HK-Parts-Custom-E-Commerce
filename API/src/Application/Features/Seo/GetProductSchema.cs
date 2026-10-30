using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Builds schema.org Product JSON-LD for a published product (resolved by slug or id). Meta title/description
/// override the display name/short description; price falls back to the cheapest enabled variant; availability
/// reflects on-hand stock (product- or variant-level) or a backorder allowance. Throws
/// <see cref="NotFoundException"/> when the product is missing or unpublished.
/// </summary>
public record GetProductSchemaQuery(string IdOrSlug) : IRequest<ProductSchemaMarkup>;

public class GetProductSchemaQueryHandler : IRequestHandler<GetProductSchemaQuery, ProductSchemaMarkup>
{
    private const string BaseCurrencyKey = "currency.base";

    private readonly IApplicationDbContext _db;
    private readonly ISeoService _seo;
    private readonly ISettingsService _settings;

    public GetProductSchemaQueryHandler(IApplicationDbContext db, ISeoService seo, ISettingsService settings)
    {
        _db = db;
        _seo = seo;
        _settings = settings;
    }

    public async Task<ProductSchemaMarkup> Handle(GetProductSchemaQuery request, CancellationToken cancellationToken)
    {
        var key = request.IdOrSlug?.Trim() ?? string.Empty;

        var query = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.IsPublished)
            .Include(p => p.Manufacturer)
            .Include(p => p.InventoryLevels)
            .Include(p => p.Variants).ThenInclude(v => v.InventoryLevels)
            .Include(p => p.Pictures).ThenInclude(pic => pic.Media);

        var product = (Guid.TryParse(key, out var id)
            ? await query.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            : await query.FirstOrDefaultAsync(p => p.Slug == key, cancellationToken))
            ?? throw new NotFoundException(nameof(Product), key);

        var currency = await _settings.GetValueAsync(BaseCurrencyKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(currency))
            currency = "USD";

        // Product-level price, falling back to the cheapest enabled priced variant for variation products.
        var price = product.Price
            ?? product.Variants
                .Where(v => v.IsEnabled && v.Price != null)
                .Select(v => v.Price)
                .Min();

        var inStock = product.AllowBackorder
            || product.StockQuantity > 0
            || product.Variants.Any(v => v.IsEnabled && v.StockQuantity > 0);

        // Primary product-level image: prefer an actual image over a video embed, then display order.
        var imageUrl = product.Pictures
            .Where(pic => pic.ProductVariantId == null && pic.Media != null)
            .OrderBy(pic => pic.Media!.MediaType == MediaType.Image ? 0 : 1)
            .ThenBy(pic => pic.DisplayOrder)
            .Select(pic => pic.Media!.Url)
            .FirstOrDefault();

        var input = new ProductSchemaInput(
            Name: string.IsNullOrWhiteSpace(product.MetaTitle) ? product.Name : product.MetaTitle!,
            Description: string.IsNullOrWhiteSpace(product.MetaDescription) ? product.ShortDescription : product.MetaDescription,
            Sku: product.Sku,
            Slug: product.Slug,
            ImageUrl: imageUrl,
            Price: price,
            PriceCurrency: currency!,
            InStock: inStock,
            Brand: product.Manufacturer?.Name);

        return _seo.BuildProductSchema(input);
    }
}
