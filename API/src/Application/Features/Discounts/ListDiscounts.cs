using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Discounts;

/// <summary>Returns a page of discount rules ordered by name, optionally filtered by name/scope/active state.</summary>
public record ListDiscountsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    DiscountScope? Scope = null,
    bool? IsActive = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<DiscountDto>>;

public class ListDiscountsQueryHandler : IRequestHandler<ListDiscountsQuery, PaginatedList<DiscountDto>>
{
    // Column name (from the grid) -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["scope"] = "Scope",
        ["type"] = "Type",
        ["value"] = "Value",
        ["isActive"] = "IsActive",
    };

    private readonly IApplicationDbContext _db;

    public ListDiscountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<DiscountDto>> Handle(ListDiscountsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Discount> query = _db.Discounts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(d => d.Name.Contains(term));
        }

        if (request.Scope.HasValue)
            query = query.Where(d => d.Scope == request.Scope.Value);

        if (request.IsActive.HasValue)
            query = query.Where(d => d.IsActive == request.IsActive.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(d => d.Name));

        var page = await PaginatedList<Discount>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(DiscountDto.From).ToList();
        return new PaginatedList<DiscountDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
