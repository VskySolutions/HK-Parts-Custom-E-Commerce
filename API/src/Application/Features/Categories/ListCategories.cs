using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

/// <summary>Returns a page of categories ordered by display order then name, optionally filtered by a name search term.</summary>
public record ListCategoriesQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<CategoryDto>>;

public class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, PaginatedList<CategoryDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["slug"] = "Slug",
        ["parent"] = "Parent.Name",
        ["displayOrder"] = "DisplayOrder",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CategoryDto>> Handle(ListCategoriesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Category> query = _db.Categories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name));

        var page = await PaginatedList<Category>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CategoryDto.From).ToList();
        return new PaginatedList<CategoryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
