using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Compact product card used across storefront listing surfaces (category, tag and manufacturer
/// pages, related sections, recently-viewed). Only buyer-facing fields plus a primary image.
/// </summary>
public class StorefrontProductSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ShortDescription { get; set; }
    public decimal? Price { get; set; }
    public Guid? ManufacturerId { get; set; }

    /// <summary>Product type (Simple/Grouped/WithVariants/…); lets the storefront card route variation products to the detail page for option selection.</summary>
    public ProductType ProductType { get; set; }

    /// <summary>Quote-only product (REQ-INQ-001): the card offers an inquiry CTA instead of add-to-cart.</summary>
    public bool IsInquiryOnly { get; set; }

    /// <summary>Per-product override for the inquiry call-to-action; null falls back to the tenant default.</summary>
    public string? InquiryButtonLabel { get; set; }

    /// <summary>
    /// URL of the primary product-level image. An actual image is preferred over a video entry
    /// (falling back to the video's thumbnail); null when the product has no product-level media.
    /// </summary>
    public string? PrimaryImageUrl { get; set; }

    /// <summary>Projects a summary; expects the product-level <see cref="Product.Images"/> to be loaded.</summary>
    public static StorefrontProductSummaryDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ShortDescription = p.ShortDescription,
        Price = p.Price,
        ManufacturerId = p.ManufacturerId,
        ProductType = p.ProductType,
        IsInquiryOnly = p.IsInquiryOnly,
        InquiryButtonLabel = p.InquiryButtonLabel,
        PrimaryImageUrl = p.Pictures
            .Where(i => i.ProductVariantId == null && i.Media != null)
            .OrderBy(i => i.Media!.MediaType == MediaType.Image ? 0 : 1)
            .ThenBy(i => i.DisplayOrder)
            .Select(i => i.Media!.Url)
            .FirstOrDefault(),
    };
}

/// <summary>
/// A node in the public storefront category tree (enabled categories only), with a published-product
/// count. Children are nested for the header mega-menu and the home category grid.
/// </summary>
public class StorefrontCategoryNodeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public Guid? ParentId { get; set; }
    public int ProductCount { get; set; }
    public List<StorefrontCategoryNodeDto> Children { get; set; } = new();
}

/// <summary>An image or video-embed gallery entry (REQ-CAT-012); variant-scoped when <see cref="ProductVariantId"/> is set.</summary>
public class StorefrontImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public MediaType MediaType { get; set; }
    public int DisplayOrder { get; set; }
    public Guid? ProductVariantId { get; set; }

    public static StorefrontImageDto From(ProductPicture i) => new()
    {
        Url = i.Media?.Url ?? string.Empty,
        AltText = i.Media?.AltText,
        MediaType = i.Media?.MediaType ?? MediaType.Image,
        DisplayOrder = i.DisplayOrder,
        ProductVariantId = i.ProductVariantId,
    };
}

/// <summary>A purchasable variant with the attribute-value combination that defines it (REQ-CAT-002).</summary>
public class StorefrontVariantDto
{
    public Guid Id { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int StockQuantity { get; set; }

    /// <summary>Whether the variant is available for purchase/selection (AC-CAT-002.4).</summary>
    public bool IsEnabled { get; set; }
    public List<Guid> AttributeValueIds { get; set; } = new();

    /// <summary>Projects a variant; expects <c>AttributeValues</c> to be loaded.</summary>
    public static StorefrontVariantDto From(ProductVariant v) => new()
    {
        Id = v.Id,
        Sku = v.Sku,
        Price = v.Price,
        StockQuantity = v.StockQuantity,
        IsEnabled = v.IsEnabled,
        AttributeValueIds = v.AttributeValues
            .Select(a => a.ProductAttributeValueId)
            .ToList(),
    };
}

/// <summary>
/// A product attribute shown on the detail page: either a variant-driving one (e.g. "Colour", "Size")
/// carrying the values actually used by the product's variants — powering the grouped option pickers
/// (Dropdown/Button/Swatch) — or a CustomInput one the buyer fills in, which carries no values and
/// uses <see cref="InputType"/>/<see cref="MaxLength"/>/<see cref="IsRequired"/> instead.
/// </summary>
public class StorefrontAttributeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProductAttributeDisplayType DisplayType { get; set; }
    public List<StorefrontAttributeValueDto> Values { get; set; } = new();

    /// <summary>CustomInput only: the kind of value the buyer types (Text or Number).</summary>
    public ProductAttributeInputType InputType { get; set; }

    /// <summary>CustomInput only: maximum length the buyer may type; null = unlimited.</summary>
    public int? MaxLength { get; set; }

    /// <summary>CustomInput only: the buyer must fill the field in before adding to the cart.</summary>
    public bool IsRequired { get; set; }
}

/// <summary>A single selectable attribute value (e.g. "Red") with an optional swatch colour.</summary>
public class StorefrontAttributeValueDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
}

/// <summary>
/// Full storefront product detail (AC-CAT-007.2): display fields, variants, product- and variant-level
/// media, specification option ids, tag names, and the related / cross-sell / up-sell sections.
/// </summary>
public class StorefrontProductDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public ProductType ProductType { get; set; }
    public string? ShortDescription { get; set; }
    public string? FullDescription { get; set; }
    public string? Sku { get; set; }
    public decimal? Price { get; set; }
    public int StockQuantity { get; set; }
    public bool AllowBackorder { get; set; }
    public DateTime? EstimatedRestockDate { get; set; }
    public Guid? ManufacturerId { get; set; }

    /// <summary>Quote-only product (REQ-INQ-001): the buyer requests a quote instead of paying.</summary>
    public bool IsInquiryOnly { get; set; }

    /// <summary>Per-product override for the inquiry call-to-action; null falls back to the tenant default.</summary>
    public string? InquiryButtonLabel { get; set; }

    // Type-specific display config.
    public GiftCardType? GiftCardType { get; set; }
    public decimal? GiftCardAmount { get; set; }
    public int? DownloadExpiryDays { get; set; }
    public int? DownloadLimit { get; set; }

    public List<StorefrontVariantDto> Variants { get; set; } = new();

    /// <summary>The variant-driving attributes (with labels + swatch colours) for the option pickers.</summary>
    public List<StorefrontAttributeDto> Attributes { get; set; } = new();

    public List<StorefrontImageDto> Images { get; set; } = new();
    public List<Guid> SpecificationOptionIds { get; set; } = new();
    public List<string> TagNames { get; set; } = new();

    public List<StorefrontProductSummaryDto> RelatedProducts { get; set; } = new();
    public List<StorefrontProductSummaryDto> CrossSells { get; set; } = new();
    public List<StorefrontProductSummaryDto> UpSells { get; set; } = new();

    /// <summary>
    /// Projects the product's own display fields, media, specs and tags. Relationship sections are
    /// resolved separately by the handler (the targets require a further published-only lookup).
    /// </summary>
    public static StorefrontProductDetailDto From(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        ProductType = p.ProductType,
        ShortDescription = p.ShortDescription,
        FullDescription = p.FullDescription,
        Sku = p.Sku,
        Price = p.Price,
        StockQuantity = p.StockQuantity,
        AllowBackorder = p.AllowBackorder,
        EstimatedRestockDate = p.EstimatedRestockDate,
        ManufacturerId = p.ManufacturerId,
        IsInquiryOnly = p.IsInquiryOnly,
        InquiryButtonLabel = p.InquiryButtonLabel,
        GiftCardType = p.GiftCardType,
        GiftCardAmount = p.GiftCardAmount,
        DownloadExpiryDays = p.DownloadExpiryDays,
        DownloadLimit = p.DownloadLimit,
        Variants = p.Variants
            .OrderBy(v => v.DisplayOrder)
            .Select(StorefrontVariantDto.From)
            .ToList(),
        Attributes = BuildAttributes(p),
        Images = p.Pictures
            .Where(i => i.Media != null)
            .OrderBy(i => i.DisplayOrder)
            .Select(StorefrontImageDto.From)
            .ToList(),
        SpecificationOptionIds = p.SpecificationValues
            .Select(s => s.SpecificationAttributeOptionId)
            .ToList(),
        TagNames = p.Tags
            .Where(t => t.ProductTag != null)
            .Select(t => t.ProductTag!.Name)
            .OrderBy(n => n)
            .ToList(),
    };

    /// <summary>
    /// The attributes the detail page renders, in attribute display order:
    /// <list type="bullet">
    /// <item>the values actually used by the product's variants, grouped into their parent attributes
    /// (Colour, Size, …) with each attribute's display type and each value's swatch colour, so the
    /// storefront can render labelled option pickers instead of raw SKUs — expects
    /// <c>Variants.AttributeValues.ProductAttributeValue.ProductAttribute</c> to be loaded;</item>
    /// <item>the product's assigned CustomInput attributes, which carry no values (the buyer types
    /// their own) and so never surface through the variants — expects
    /// <c>AttributeMappings.ProductAttribute</c> to be loaded.</item>
    /// </list>
    /// </summary>
    private static List<StorefrontAttributeDto> BuildAttributes(Product p)
    {
        var values = p.Variants
            .SelectMany(v => v.AttributeValues)
            .Select(av => av.ProductAttributeValue)
            .Where(pav => pav != null && pav.ProductAttribute != null)
            .Select(pav => pav!)
            .ToList();

        var variantDriven = values
            .GroupBy(pav => pav.ProductAttributeId)
            .Select(g =>
            {
                var attr = g.First().ProductAttribute!;
                return new
                {
                    attr.DisplayOrder,
                    Dto = new StorefrontAttributeDto
                    {
                        Id = attr.Id,
                        Name = attr.Name,
                        DisplayType = attr.DisplayType,
                        Values = g
                            .GroupBy(pav => pav.Id)
                            .Select(vg => vg.First())
                            .OrderBy(pav => pav.DisplayOrder)
                            .ThenBy(pav => pav.Value)
                            .Select(pav => new StorefrontAttributeValueDto
                            {
                                Id = pav.Id,
                                Value = pav.Value,
                                ColorHex = pav.ColorHex,
                            })
                            .ToList(),
                    },
                };
            });

        var customInputs = p.AttributeMappings
            .Select(m => m.ProductAttribute)
            .Where(a => a != null && a.DisplayType == ProductAttributeDisplayType.CustomInput)
            .Select(a => a!)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .Select(attr => new
            {
                attr.DisplayOrder,
                Dto = new StorefrontAttributeDto
                {
                    Id = attr.Id,
                    Name = attr.Name,
                    DisplayType = attr.DisplayType,
                    InputType = attr.InputType,
                    MaxLength = attr.MaxLength,
                    IsRequired = attr.IsRequired,
                },
            });

        return variantDriven
            .Concat(customInputs)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Dto.Name)
            .Select(x => x.Dto)
            .ToList();
    }
}

/// <summary>A specification attribute present in a product set, with the options actually present (AC-STF-003.1).</summary>
public class FilterableSpecDto
{
    public Guid SpecificationAttributeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<FilterableSpecOptionDto> Options { get; set; } = new();
}

/// <summary>A single option value available for a <see cref="FilterableSpecDto"/>.</summary>
public class FilterableSpecOptionDto
{
    public Guid SpecificationAttributeOptionId { get; set; }
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// A storefront category landing page: the (enabled) category's SEO/display info, a page of its
/// published products, and the filterable specification attributes present in that product set.
/// </summary>
public class CategoryPageDto
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public Guid? ParentId { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaKeywords { get; set; }

    public List<StorefrontProductSummaryDto> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }

    public List<FilterableSpecDto> Filters { get; set; } = new();
}

/// <summary>
/// Side-by-side comparison payload (AC-STF-005.2/3): the union of specification attributes acts as the
/// row headers; each product carries its price plus the specification values it has for those rows.
/// </summary>
public class ComparisonDto
{
    /// <summary>The union of specification attributes present across the compared products, in display order.</summary>
    public List<ComparisonAttributeDto> Attributes { get; set; } = new();
    public List<ComparisonProductDto> Products { get; set; } = new();
}

/// <summary>A comparison row header (a specification attribute present in the set).</summary>
public class ComparisonAttributeDto
{
    public Guid SpecificationAttributeId { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>A single compared product column: identity, price, primary image and its specification values.</summary>
public class ComparisonProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public decimal? Price { get; set; }
    public Guid? ManufacturerId { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public List<ComparisonSpecValueDto> SpecificationValues { get; set; } = new();
}

/// <summary>One product's value for a specification attribute, keyed by attribute for row alignment.</summary>
public class ComparisonSpecValueDto
{
    public Guid SpecificationAttributeId { get; set; }
    public Guid SpecificationAttributeOptionId { get; set; }
    public string Value { get; set; } = string.Empty;
}
