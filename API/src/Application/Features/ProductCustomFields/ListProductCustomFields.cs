using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ProductCustomFields;

/// <summary>Returns every product custom field (a small, admin-curated list) by display order then name.</summary>
public record ListProductCustomFieldsQuery(bool? IsActive = null, int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, bool SortDescending = false) : IRequest<PaginatedList<ProductCustomFieldDto>>;

public class ListProductCustomFieldsQueryHandler : IRequestHandler<ListProductCustomFieldsQuery, PaginatedList<ProductCustomFieldDto>>
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["code"] = "Code",
        ["displayOrder"] = "DisplayOrder",
        ["isEnabled"] = "IsEnabled",
    };

    private readonly IApplicationDbContext _db;

    public ListProductCustomFieldsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ProductCustomFieldDto>> Handle(ListProductCustomFieldsQuery request, CancellationToken cancellationToken)
    {

        IQueryable<ProductCustomField> query = _db.ProductCustomFields.AsNoTracking();

        if (request.IsActive.HasValue)
            query = query.Where(f => f.IsActive == request.IsActive.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(f => f.Name.Contains(term) || f.Code.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name));

        var page = await PaginatedList<ProductCustomField>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ProductCustomFieldDto.From).ToList();
        return new PaginatedList<ProductCustomFieldDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
