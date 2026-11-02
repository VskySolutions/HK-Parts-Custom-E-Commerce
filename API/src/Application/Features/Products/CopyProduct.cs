using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>
/// Duplicates a product (the PHP admin's "Copy" action). The copy is a new, unpublished draft named
/// "{name} (Copy)" with its own unique slug ("{name}-copy", "-copy-2"…) and a "-COPY" SKU, carrying over every setting and assignment:
/// scalars, categories, tags, images/videos (same Media assets), attributes + variants (with their
/// attribute values and tier prices), specification values, tier prices, related/grouped products,
/// downloads, attachments, store listing, custom field values and the SEO row (minus the canonical URL).
/// Stock is NOT copied — the copy starts at zero on-hand, as does each copied variant.
/// </summary>
public record CopyProductCommand(Guid ProductId) : IRequest<ProductDto>;

public class CopyProductCommandHandler : IRequestHandler<CopyProductCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public CopyProductCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(CopyProductCommand request, CancellationToken cancellationToken)
    {
        var src = await _db.Products.AsNoTracking()
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var copyName = Truncate($"{src.Name} (Copy)", 400);
        var copy = new Product
        {
            Name = copyName,
            Slug = await NextSlugAsync(copyName, cancellationToken),
            Sku = await NextSkuAsync(src.Sku, cancellationToken),
            ProductType = src.ProductType,
            ShortDescription = src.ShortDescription,
            FullDescription = src.FullDescription,
            Price = src.Price,
            Gtin = null, // a barcode identifies one product — never duplicate it
            Cost = src.Cost,
            Msrp = src.Msrp,
            SalePrice = src.SalePrice,
            SaleStartDateUtc = src.SaleStartDateUtc,
            SaleEndDateUtc = src.SaleEndDateUtc,
            TaxCode = src.TaxCode,
            IsPurchasable = src.IsPurchasable,
            IsFflRequired = src.IsFflRequired,
            MinOrderQuantity = src.MinOrderQuantity,
            MaxOrderQuantity = src.MaxOrderQuantity,
            StockStatusOverride = src.StockStatusOverride,
            BackorderMessage = src.BackorderMessage,
            Weight = src.Weight,
            Length = src.Length,
            Width = src.Width,
            Height = src.Height,
            PackagePieces = src.PackagePieces,
            PackageType = src.PackageType,
            FreightClass = src.FreightClass,
            IsHazardous = src.IsHazardous,
            AllowBackorder = src.AllowBackorder,
            EstimatedRestockDate = src.EstimatedRestockDate,
            TaxCategoryId = src.TaxCategoryId,
            ManufacturerId = src.ManufacturerId,
            IsPublished = false,
            ReviewsEnabled = src.ReviewsEnabled,
            IsInquiryOnly = src.IsInquiryOnly,
            InquiryButtonLabel = src.InquiryButtonLabel,
            DisplayOrder = src.DisplayOrder,
            IsFeatured = false,
            FeaturedDisplayOrder = src.FeaturedDisplayOrder,
            IsSubscribable = src.IsSubscribable,
            SubscriptionIntervals = src.SubscriptionIntervals,
            DownloadExpiryDays = src.DownloadExpiryDays,
            DownloadLimit = src.DownloadLimit,
            GiftCardType = src.GiftCardType,
            GiftCardAmount = src.GiftCardAmount,
        };

        foreach (var c in src.ProductCategories)
            copy.ProductCategories.Add(new ProductCategory { CategoryId = c.CategoryId, DisplayOrder = c.DisplayOrder });
        foreach (var t in src.Tags)
            copy.Tags.Add(new ProductTagMapping { ProductTagId = t.ProductTagId });
        foreach (var a in src.AttributeMappings)
            copy.AttributeMappings.Add(new ProductAttributeMapping { ProductAttributeId = a.ProductAttributeId, DisplayOrder = a.DisplayOrder });
        foreach (var s in src.SpecificationValues)
            copy.SpecificationValues.Add(new ProductSpecificationValue { SpecificationAttributeOptionId = s.SpecificationAttributeOptionId });
        foreach (var t in src.TierPrices.Where(t => t.ProductVariantId == null))
            copy.TierPrices.Add(new TierPrice { MinQuantity = t.MinQuantity, Price = t.Price });
        foreach (var r in src.Relationships)
            copy.Relationships.Add(new ProductRelationship { RelatedProductId = r.RelatedProductId, RelationshipType = r.RelationshipType, DisplayOrder = r.DisplayOrder });
        foreach (var g in src.GroupedMembers)
            copy.GroupedMembers.Add(new GroupedProductMember { MemberProductId = g.MemberProductId, DisplayOrder = g.DisplayOrder });
        foreach (var d in src.Downloads)
            copy.Downloads.Add(new ProductDownload { Name = d.Name, Url = d.Url, FileKey = d.FileKey, DisplayOrder = d.DisplayOrder });
        foreach (var a in src.Attachments)
            copy.Attachments.Add(new ProductAttachment { MediaId = a.MediaId, Title = a.Title, DisplayOrder = a.DisplayOrder });
        foreach (var s in src.StoreMappings)
            copy.StoreMappings.Add(new ProductStoreMapping { StoreId = s.StoreId });
        foreach (var v in src.CustomFieldValues)
            copy.CustomFieldValues.Add(new ProductCustomFieldValue { ProductCustomFieldId = v.ProductCustomFieldId, Value = v.Value });

        // Variants first, so variant-scoped pictures can point at the matching copied variant.
        var variantMap = new Dictionary<Guid, ProductVariant>();
        foreach (var v in src.Variants.Where(v => !v.Deleted))
        {
            var nv = new ProductVariant
            {
                Sku = string.IsNullOrWhiteSpace(v.Sku) ? null : Truncate(v.Sku + "-COPY", 400),
                Price = v.Price,
                Gtin = null,
                Cost = v.Cost,
                Msrp = v.Msrp,
                Weight = v.Weight,
                AllowBackorder = v.AllowBackorder,
                EstimatedRestockDate = v.EstimatedRestockDate,
                IsEnabled = v.IsEnabled,
                DisplayOrder = v.DisplayOrder,
            };
            foreach (var av in v.AttributeValues)
                nv.AttributeValues.Add(new ProductVariantAttributeValue { ProductAttributeValueId = av.ProductAttributeValueId });
            foreach (var t in v.TierPrices)
                nv.TierPrices.Add(new TierPrice { MinQuantity = t.MinQuantity, Price = t.Price });
            copy.Variants.Add(nv);
            variantMap[v.Id] = nv;
        }

        foreach (var pic in src.Pictures)
        {
            var np = new ProductPicture { MediaId = pic.MediaId, DisplayOrder = pic.DisplayOrder };
            if (pic.ProductVariantId is Guid vid)
            {
                if (!variantMap.TryGetValue(vid, out var nv)) continue; // picture of a deleted variant
                np.ProductVariant = nv;
            }
            copy.Pictures.Add(np);
        }

        _db.Products.Add(copy);

        var seo = await _db.SeoMetadata.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EntityType == Domain.Enums.SeoEntityType.Product && s.EntityId == src.Id, cancellationToken);
        if (seo is not null)
        {
            _db.SeoMetadata.Add(new SeoMetadata
            {
                EntityType = seo.EntityType,
                EntityId = copy.Id,
                MetaTitle = seo.MetaTitle,
                MetaDescription = seo.MetaDescription,
                MetaKeywords = seo.MetaKeywords,
                CanonicalUrl = null, // the source's canonical would point search engines away from the copy
                H1 = seo.H1,
                MetaImageMediaId = seo.MetaImageMediaId,
                MetaOpenGraph = seo.MetaOpenGraph,
                MetaCustomTags = seo.MetaCustomTags,
                MetaFooterTags = seo.MetaFooterTags,
                NoIndex = seo.NoIndex,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var saved = await _db.Products.AsNoTracking()
            .AsSplitQuery()
            .WithFullGraph()
            .FirstAsync(p => p.Id == copy.Id, cancellationToken);
        return ProductDto.From(saved);
    }

    // "Widget (Copy)" -> "widget-copy", then "widget-copy-2"… until unused, so the copy has a working
    // storefront URL straight away (the slug is unique per product — see CreateProduct).
    private async Task<string> NextSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Truncate(SlugGenerator.Generate(name), 390);
        var candidate = baseSlug;
        for (var n = 2; await _db.Products.AnyAsync(p => p.Slug == candidate, ct); n++)
            candidate = $"{baseSlug}-{n}";
        return candidate;
    }

    // "ABC" -> "ABC-COPY", then "ABC-COPY-2", "ABC-COPY-3"… until unused. No SKU stays no SKU.
    private async Task<string?> NextSkuAsync(string? sku, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;
        var baseSku = Truncate(sku.Trim() + "-COPY", 390);
        var candidate = baseSku;
        for (var n = 2; await _db.Products.AnyAsync(p => p.Sku == candidate, ct); n++)
            candidate = $"{baseSku}-{n}";
        return candidate;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
