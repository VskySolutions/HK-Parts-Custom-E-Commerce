using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.SpecificationAttributes;

/// <summary>Returns a page of specification attributes ordered by display order then name, optionally filtered by a name search term and/or filterable state.</summary>
public record ListSpecificationAttributesQuery(int Page = 1, int PageSize = 20, string? Search = null, bool? IsFilterable = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<SpecificationAttributeDto>>;

public class ListSpecificationAttributesQueryHandler : IRequestHandler<ListSpecificationAttributesQuery, PaginatedList<SpecificationAttributeDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["isFilterable"] = "IsFilterable",
    };

    private readonly IApplicationDbContext _db;

    public ListSpecificationAttributesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<SpecificationAttributeDto>> Handle(ListSpecificationAttributesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<SpecificationAttribute> query = _db.SpecificationAttributes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a => a.Name.Contains(term));
        }

        if (request.IsFilterable.HasValue)
            query = query.Where(a => a.IsFilterable == request.IsFilterable.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(a => a.DisplayOrder).ThenBy(a => a.Name));

        var page = await PaginatedList<SpecificationAttribute>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(SpecificationAttributeDto.From).ToList();
        return new PaginatedList<SpecificationAttributeDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
