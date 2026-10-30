using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.TaxCategories;

/// <summary>
/// Returns a page of tax categories ordered by display order then name, optionally filtered by name
/// or restricted to active categories. Backs the product form's Tax Category picker (AC-CAT-001.6).
/// </summary>
public record ListTaxCategoriesQuery(int Page = 1, int PageSize = 200, string? Search = null, bool? ActiveOnly = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<TaxCategoryDto>>;

public class ListTaxCategoriesQueryHandler : IRequestHandler<ListTaxCategoriesQuery, PaginatedList<TaxCategoryDto>>
{
    // Sortable field -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["defaultRatePercent"] = "DefaultRatePercent",
        ["displayOrder"] = "DisplayOrder",
        ["isActive"] = "IsActive",
    };

    private readonly IApplicationDbContext _db;

    public ListTaxCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<TaxCategoryDto>> Handle(ListTaxCategoriesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<TaxCategory> query = _db.TaxCategories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(t => t.Name.Contains(term));
        }

        if (request.ActiveOnly == true)
            query = query.Where(t => t.IsActive);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(t => t.DisplayOrder).ThenBy(t => t.Name));

        var page = await PaginatedList<TaxCategory>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(TaxCategoryDto.From).ToList();
        return new PaginatedList<TaxCategoryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
