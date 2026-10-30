using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsProductCollections;

/// <summary>Returns a page of product collections (ordered by name by default), optionally filtered by a
/// name/slug search term and enabled state. Each row carries its product count and last-updated time.</summary>
public record ListCmsProductCollectionsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsEnabled = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<CmsProductCollectionListItemDto>>;

public class ListCmsProductCollectionsQueryHandler : IRequestHandler<ListCmsProductCollectionsQuery, PaginatedList<CmsProductCollectionListItemDto>>
{
    // Grid column name -> entity property path. Anything else falls back to the natural order below.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["name"] = "Name",
        ["slug"] = "Slug",
        ["isEnabled"] = "IsEnabled",
        ["createdOnUtc"] = "CreatedOnUtc",
        ["updatedOnUtc"] = "UpdatedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListCmsProductCollectionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CmsProductCollectionListItemDto>> Handle(ListCmsProductCollectionsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CMSProductCollection> query = _db.CMSProductCollections.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Slug != null && c.Slug.Contains(term)));
        }

        if (request.IsEnabled.HasValue)
            query = query.Where(c => c.IsEnabled == request.IsEnabled.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderBy(c => c.Name));

        // Project in SQL so ProductCount is a COUNT sub-query rather than loading every item row to count it.
        var projected = ordered.Select(c => new CmsProductCollectionListItemDto
        {
            Id = c.Id,
            Name = c.Name,
            Slug = c.Slug,
            IsEnabled = c.IsEnabled,
            ProductCount = c.Items.Count,
            UpdatedOnUtc = c.UpdatedOnUtc,
        });

        return await PaginatedList<CmsProductCollectionListItemDto>.CreateAsync(projected, request.Page, request.PageSize, cancellationToken);
    }
}
