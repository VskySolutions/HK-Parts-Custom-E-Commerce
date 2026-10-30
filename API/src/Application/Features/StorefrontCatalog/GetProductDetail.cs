using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Returns the full published product detail (resolved by id or slug) with variants, product- and
/// variant-level media, specification option ids, tag names, and the related / cross-sell / up-sell
/// sections resolved to published summaries (AC-CAT-007.2). Throws <see cref="NotFoundException"/>
/// when the product is missing or unpublished.
/// </summary>
public record GetProductDetailQuery(string IdOrSlug) : IRequest<StorefrontProductDetailDto>;

public class GetProductDetailQueryHandler : IRequestHandler<GetProductDetailQuery, StorefrontProductDetailDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public GetProductDetailQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<StorefrontProductDetailDto> Handle(GetProductDetailQuery request, CancellationToken cancellationToken)
    {
        var key = request.IdOrSlug?.Trim() ?? string.Empty;

        // Product- and variant-level pictures both carry ProductId, so p.Pictures loads the full gallery.
        var query = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Where(p => p.IsPublished)
            .Include(p => p.InventoryLevels)
            .Include(p => p.Variants).ThenInclude(v => v.InventoryLevels)
            .Include(p => p.Variants).ThenInclude(v => v.AttributeValues)
                .ThenInclude(av => av.ProductAttributeValue).ThenInclude(pav => pav!.ProductAttribute)
            // CustomInput attributes drive no variants, so they only reach the DTO through the mappings.
            .Include(p => p.AttributeMappings).ThenInclude(m => m.ProductAttribute)
            .Include(p => p.Pictures).ThenInclude(pic => pic.Media)
            .Include(p => p.SpecificationValues)
            .Include(p => p.Tags).ThenInclude(t => t.ProductTag)
            .Include(p => p.Relationships);

        var product = (Guid.TryParse(key, out var productId)
            ? await query.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            : await query.FirstOrDefaultAsync(p => p.Slug == key, cancellationToken))
            ?? throw new NotFoundException(nameof(Product), key);

        var dto = StorefrontProductDetailDto.From(product);

        // A group member must see their price on the detail page, not just in the cart (AC-CUS-003.5).
        // The group is resolved once here and reused for the product, its variants and the relationship
        // sections below; every overlay is a no-op for guests, so the anonymous path is unchanged.
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);

        // The product's own (product-level) price: no variant to key on.
        await _groups.ApplyGroupPricingAsync(
            new[] { dto },
            groupId,
            d => d.Price is decimal price ? new GroupPriceRequest(d.Id, null, price) : null,
            (d, price) => d.Price = price,
            cancellationToken);

        // Each variant keys on its own id so a variant-specific fixed group price overrides the
        // product-level one (AC-CUS-003.4).
        await _groups.ApplyGroupPricingAsync(
            dto.Variants,
            groupId,
            v => v.Price is decimal price ? new GroupPriceRequest(product.Id, v.Id, price) : null,
            (v, price) => v.Price = price,
            cancellationToken);

        // Resolve relationship targets to published summaries; unpublished/soft-deleted targets drop out.
        var relatedIds = product.Relationships.Select(r => r.RelatedProductId).Distinct().ToList();
        if (relatedIds.Count > 0)
        {
            var targets = await _db.Products
                .AsNoTracking()
                .Published()
                .Where(p => relatedIds.Contains(p.Id))
                .WithSummaryImages()
                .ToListAsync(cancellationToken);

            var byId = targets.ToDictionary(p => p.Id, StorefrontProductSummaryDto.From);

            // The related/cross-sell/up-sell cards are the same summaries the listing pages show, so they
            // carry the same overlay. Applied over byId (not per section) so a product appearing in two
            // sections — the sections share the DTO instance — is resolved once.
            await _groups.ApplyGroupPricingAsync(
                byId.Values,
                groupId,
                i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
                (i, price) => i.Price = price,
                cancellationToken);

            List<StorefrontProductSummaryDto> Section(ProductRelationshipType type) => product.Relationships
                .Where(r => r.RelationshipType == type && byId.ContainsKey(r.RelatedProductId))
                .OrderBy(r => r.DisplayOrder)
                .Select(r => byId[r.RelatedProductId])
                .ToList();

            dto.RelatedProducts = Section(ProductRelationshipType.Related);
            dto.CrossSells = Section(ProductRelationshipType.CrossSell);
            dto.UpSells = Section(ProductRelationshipType.UpSell);
        }

        return dto;
    }
}
