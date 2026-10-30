using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StorefrontCatalog;

/// <summary>
/// Returns the storefront category page for an enabled category (resolved by id or slug): a sortable
/// page of its published products plus the filterable specification attributes present in that product
/// set (AC-STF-003.1). Throws <see cref="NotFoundException"/> when the category is missing or disabled.
/// </summary>
public record GetCategoryPageQuery(
    string IdOrSlug,
    int Page = 1,
    int PageSize = 20,
    string? Sort = null) : IRequest<CategoryPageDto>;

public class GetCategoryPageQueryHandler : IRequestHandler<GetCategoryPageQuery, CategoryPageDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICustomerGroupService _groups;

    public GetCategoryPageQueryHandler(IApplicationDbContext db, ICustomerGroupService groups)
    {
        _db = db;
        _groups = groups;
    }

    public async Task<CategoryPageDto> Handle(GetCategoryPageQuery request, CancellationToken cancellationToken)
    {
        var key = request.IdOrSlug?.Trim() ?? string.Empty;

        var enabled = _db.Categories.AsNoTracking().Where(c => c.IsEnabled);
        var category = (Guid.TryParse(key, out var categoryId)
            ? await enabled.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            : await enabled.FirstOrDefaultAsync(c => c.Slug == key, cancellationToken))
            ?? throw new NotFoundException(nameof(Category), key);

        var productsQuery = _db.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Published()
            .Where(p => p.ProductCategories.Any(pc => pc.CategoryId == category.Id))
            .WithSummaryImages()
            .ApplyStorefrontSort(request.Sort);

        var page = await PaginatedList<Product>.CreateAsync(productsQuery, request.Page, request.PageSize, cancellationToken);
        var filters = await BuildFiltersAsync(category.Id, cancellationToken);

        // A group member must see their price while browsing, not just in the cart (AC-CUS-003.5). Overlaid
        // on the projected page in one batch; a no-op for guests, so the anonymous path is unchanged.
        // Summaries are product-level cards, so there is no variant to key on.
        var items = page.Items.Select(StorefrontProductSummaryDto.From).ToList();
        var groupId = await _groups.GetCurrentGroupIdAsync(cancellationToken);
        await _groups.ApplyGroupPricingAsync(
            items,
            groupId,
            i => i.Price is decimal price ? new GroupPriceRequest(i.Id, null, price) : null,
            (i, price) => i.Price = price,
            cancellationToken);

        return new CategoryPageDto
        {
            CategoryId = category.Id,
            Name = category.Name,
            Slug = category.Slug,
            Description = category.Description,
            ParentId = category.ParentId,
            MetaTitle = category.MetaTitle,
            MetaDescription = category.MetaDescription,
            MetaKeywords = category.MetaKeywords,
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalPages = page.TotalPages,
            TotalCount = page.TotalCount,
            Filters = filters,
        };
    }

    /// <summary>
    /// Collects the filterable specification attributes (and the distinct options actually present)
    /// across every published product in the category — computed over the whole set, not just the page.
    /// </summary>
    private async Task<List<FilterableSpecDto>> BuildFiltersAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        var rows = await _db.ProductSpecificationValues
            .AsNoTracking()
            .Where(sv =>
                sv.Product!.IsPublished
                && sv.Product.ProductCategories.Any(pc => pc.CategoryId == categoryId)
                && sv.SpecificationAttributeOption!.SpecificationAttribute!.IsFilterable)
            .Select(sv => new
            {
                AttributeId = sv.SpecificationAttributeOption!.SpecificationAttributeId,
                AttributeName = sv.SpecificationAttributeOption.SpecificationAttribute!.Name,
                AttributeOrder = sv.SpecificationAttributeOption.SpecificationAttribute.DisplayOrder,
                OptionId = sv.SpecificationAttributeOptionId,
                OptionValue = sv.SpecificationAttributeOption.Value,
                OptionOrder = sv.SpecificationAttributeOption.DisplayOrder,
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => new { r.AttributeId, r.AttributeName, r.AttributeOrder })
            .OrderBy(g => g.Key.AttributeOrder)
            .ThenBy(g => g.Key.AttributeName)
            .Select(g => new FilterableSpecDto
            {
                SpecificationAttributeId = g.Key.AttributeId,
                Name = g.Key.AttributeName,
                Options = g
                    .OrderBy(o => o.OptionOrder)
                    .ThenBy(o => o.OptionValue)
                    .Select(o => new FilterableSpecOptionDto
                    {
                        SpecificationAttributeOptionId = o.OptionId,
                        Value = o.OptionValue,
                    })
                    .ToList(),
            })
            .ToList();
    }
}
