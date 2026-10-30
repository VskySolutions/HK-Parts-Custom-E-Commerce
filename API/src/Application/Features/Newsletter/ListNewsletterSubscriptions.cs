using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Newsletter;

/// <summary>Admin paged list of newsletter subscribers (WO-56), filterable by status and email search.
/// Each row is annotated with whether the email sits in the marketing-suppression list.</summary>
public record ListNewsletterSubscriptionsQuery(
    int Page = 1,
    int PageSize = 20,
    NewsletterSubscriptionStatus? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<NewsletterSubscriptionDto>>;

public class ListNewsletterSubscriptionsQueryHandler
    : IRequestHandler<ListNewsletterSubscriptionsQuery, PaginatedList<NewsletterSubscriptionDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "Email",
        ["status"] = "Status",
        ["source"] = "Source",
        ["createdOnUtc"] = "CreatedOnUtc",
        ["confirmedOnUtc"] = "ConfirmedOnUtc",
    };

    private readonly IApplicationDbContext _db;

    public ListNewsletterSubscriptionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<NewsletterSubscriptionDto>> Handle(ListNewsletterSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<CMSNewsletterSubscription> query = _db.CMSNewsletterSubscriptions.AsNoTracking();

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.Email.Contains(term));
        }

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap,
            defaultSort: q => q.OrderByDescending(s => s.CreatedOnUtc));

        var page = await PaginatedList<CMSNewsletterSubscription>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);

        // Resolve suppression state for just this page's emails in one lookup (LEFT-JOIN equivalent).
        var emails = page.Items.Select(s => s.Email).ToList();
        var suppressedEmails = await _db.MarketingSuppressions.AsNoTracking()
            .Where(m => emails.Contains(m.RecipientEmail))
            .Select(m => m.RecipientEmail)
            .ToListAsync(cancellationToken);
        var suppressedSet = new HashSet<string>(suppressedEmails, StringComparer.OrdinalIgnoreCase);

        var items = page.Items
            .Select(s => NewsletterSubscriptionDto.From(s, suppressedSet.Contains(s.Email)))
            .ToList();

        return new PaginatedList<NewsletterSubscriptionDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
