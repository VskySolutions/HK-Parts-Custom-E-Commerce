using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ProductReviews;

/// <summary>
/// Admin moderation list of product reviews (WO-14), newest first, filterable by status, product, star
/// rating, submission date range, and a free-text term matched against the reviewer name or review body.
/// </summary>
public record ListProductReviewsQuery(
    ReviewStatus? Status = null,
    Guid? ProductId = null,
    int? Rating = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<ProductReviewDto>>;

public class ListProductReviewsQueryHandler : IRequestHandler<ListProductReviewsQuery, PaginatedList<ProductReviewDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["reviewerName"] = "ReviewerName",
        ["rating"] = "Rating",
        ["status"] = "Status",
        ["createdOnUtc"] = "CreatedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListProductReviewsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ProductReviewDto>> Handle(ListProductReviewsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ProductReview> query = _db.ProductReviews.AsNoTracking().Include(r => r.Product);

        if (request.Status is ReviewStatus status)
            query = query.Where(r => r.Status == status);

        if (request.ProductId is Guid productId)
            query = query.Where(r => r.ProductId == productId);

        if (request.Rating is int rating)
            query = query.Where(r => r.Rating == rating);

        if (request.DateFrom is DateTime from)
            query = query.Where(r => r.CreatedOnUtc >= from);

        if (request.DateTo is DateTime to)
            query = query.Where(r => r.CreatedOnUtc <= to);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(r => r.ReviewerName.Contains(term) || r.Body.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(r => r.CreatedOnUtc));

        var page = await PaginatedList<ProductReview>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ProductReviewDto.From).ToList();
        return new PaginatedList<ProductReviewDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
