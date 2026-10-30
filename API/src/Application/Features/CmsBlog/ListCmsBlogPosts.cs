using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CmsBlog;

/// <summary>Admin list of blog posts, newest first, filterable by status and a free-text term matched
/// against the title or slug.</summary>
public record ListCmsBlogPostsQuery(
    CmsContentStatus? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<CmsBlogPostDto>>;

public class ListCmsBlogPostsQueryHandler : IRequestHandler<ListCmsBlogPostsQuery, PaginatedList<CmsBlogPostDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["title"] = "Title",
        ["slug"] = "Slug",
        ["status"] = "Status",
        ["publishedOnUtc"] = "PublishedOnUtc",
        ["createdOnUtc"] = "CreatedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListCmsBlogPostsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CmsBlogPostDto>> Handle(ListCmsBlogPostsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CMSBlogPost> query = _db.CMSBlogPosts.AsNoTracking().Include(p => p.FeaturedImageMedia);

        if (request.Status is CmsContentStatus status)
            query = query.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Title.Contains(term) || p.Slug.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(p => p.CreatedOnUtc));

        var page = await PaginatedList<CMSBlogPost>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(CmsBlogPostDto.From).ToList();
        return new PaginatedList<CmsBlogPostDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
