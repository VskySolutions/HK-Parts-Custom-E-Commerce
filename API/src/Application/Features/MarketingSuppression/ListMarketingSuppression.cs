using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using MarketingSuppressionEntity = HKPartsEcommerce.Domain.Entities.MarketingSuppression;

namespace HKPartsEcommerce.Application.Features.MarketingSuppression;

/// <summary>Admin paged list of marketing-suppressed recipients (WO-87 AC-ENT-006.5), filterable by an email
/// search term. Defaults to most-recently-suppressed first.</summary>
public record ListMarketingSuppressionQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<MarketingSuppressionDto>>;

public class ListMarketingSuppressionQueryHandler
    : IRequestHandler<ListMarketingSuppressionQuery, PaginatedList<MarketingSuppressionDto>>
{
    // Grid column name -> entity property path. Anything else falls back to SuppressedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "RecipientEmail",
        ["recipientEmail"] = "RecipientEmail",
        ["suppressedOnUtc"] = "SuppressedOnUtc",
        ["source"] = "Source",
    };

    private readonly IApplicationDbContext _db;

    public ListMarketingSuppressionQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<MarketingSuppressionDto>> Handle(ListMarketingSuppressionQuery request, CancellationToken cancellationToken)
    {
        IQueryable<MarketingSuppressionEntity> query = _db.MarketingSuppressions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.RecipientEmail.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(s => s.SuppressedOnUtc));

        var page = await PaginatedList<MarketingSuppressionEntity>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(MarketingSuppressionDto.From).ToList();
        return new PaginatedList<MarketingSuppressionDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
