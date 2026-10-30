using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Media;

/// <summary>
/// The media library (WO-122): a page of committed media, searchable by SEO file name / alt text and
/// filterable by media type, newest first. Each row's public URL is resolved at read time.
/// </summary>
public record ListMediaQuery(int Page = 1, int PageSize = 24, string? Search = null, string? MediaType = null, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<MediaDto>>;

public class ListMediaQueryHandler : IRequestHandler<ListMediaQuery, PaginatedList<MediaDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["originalFileName"] = "OriginalFileName",
        ["seoFileName"] = "SeoFileName",
        ["mediaType"] = "MediaType",
        ["fileSizeBytes"] = "FileSizeBytes",
        ["createdOnUtc"] = "CreatedOnUtc",
    };

    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public ListMediaQueryHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<PaginatedList<MediaDto>> Handle(ListMediaQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Media.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(m => m.SeoFileName.Contains(term)
                || (m.AltText != null && m.AltText.Contains(term))
                || m.OriginalFileName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.MediaType)
            && Enum.TryParse<MediaType>(request.MediaType, ignoreCase: true, out var type))
        {
            query = query.Where(m => m.MediaType == type);
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap);
        var page = await PaginatedList<Domain.Entities.Media>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        var items = new List<MediaDto>(page.Items.Count);
        foreach (var m in page.Items)
        {
            var url = await _storage.GetFileUrlAsync(m.AssetKey, cancellationToken);
            items.Add(MediaDto.From(m, url));
        }

        return new PaginatedList<MediaDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
