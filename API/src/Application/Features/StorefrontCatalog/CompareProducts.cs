using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Builds a side-by-side comparison (AC-STF-005.2/3) of the given published products: their prices
/// plus specification values, with the union of specification attributes as the row headers. Unknown,
/// unpublished or soft-deleted ids are ignored and the caller-supplied order is preserved.
/// </summary>
public record CompareProductsQuery(List<Guid> ProductIds) : IRequest<ComparisonDto>;

public class CompareProductsQueryHandler : IRequestHandler<CompareProductsQuery, ComparisonDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public CompareProductsQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<ComparisonDto> Handle(CompareProductsQuery request, CancellationToken cancellationToken)
    {
        var orderedIds = (request.ProductIds ?? new List<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (orderedIds.Count == 0)
            return new ComparisonDto();

        var products = await _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Published()
            .Where(p => orderedIds.Contains(p.Id))
            .Include(p => p.Pictures.Where(i => i.ProductVariantId == null)).ThenInclude(pic => pic.Media)
            .Include(p => p.SpecificationValues)
                .ThenInclude(sv => sv.SpecificationAttributeOption)
                .ThenInclude(o => o!.SpecificationAttribute)
            .ToListAsync(cancellationToken);

        var byId = products.ToDictionary(p => p.Id);

        // Products in the caller's order (skipping any that were not found / not published).
        var comparisonProducts = new List<ComparisonProductDto>();
        foreach (var id in orderedIds)
        {
            if (!byId.TryGetValue(id, out var p))
                continue;

            comparisonProducts.Add(new ComparisonProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Slug = p.Slug,
                Price = p.Price,
                ManufacturerId = p.ManufacturerId,
                PrimaryImageUrl = StorefrontProductSummaryDto.From(p).PrimaryImageUrl,
                SpecificationValues = p.SpecificationValues
                    .Where(sv => sv.SpecificationAttributeOption?.SpecificationAttribute != null)
                    .Select(sv => new ComparisonSpecValueDto
                    {
                        SpecificationAttributeId = sv.SpecificationAttributeOption!.SpecificationAttributeId,
                        SpecificationAttributeOptionId = sv.SpecificationAttributeOptionId,
                        Value = sv.SpecificationAttributeOption.Value,
                    })
                    .ToList(),
            });
        }

        // A group member compares their prices, not list prices (AC-CUS-003.5). Overlaid on the projected
        // columns in one batch; a no-op for guests. Comparison is product-level, so there is no variant to
        // key on.
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            comparisonProducts,
            groupId,
            c => c.Price is decimal price ? new GroupPriceRequest(c.Id, null, price) : null,
            (c, price) => c.Price = price,
            cancellationToken);

        // Union of the specification attributes present across the set, in attribute display order.
        var attributes = products
            .SelectMany(p => p.SpecificationValues)
            .Where(sv => sv.SpecificationAttributeOption?.SpecificationAttribute != null)
            .Select(sv => sv.SpecificationAttributeOption!.SpecificationAttribute!)
            .GroupBy(a => a.Id)
            .Select(g => g.First())
            .OrderBy(a => a.DisplayOrder)
            .ThenBy(a => a.Name)
            .Select(a => new ComparisonAttributeDto
            {
                SpecificationAttributeId = a.Id,
                Name = a.Name,
            })
            .ToList();

        return new ComparisonDto
        {
            Attributes = attributes,
            Products = comparisonProducts,
        };
    }
}
