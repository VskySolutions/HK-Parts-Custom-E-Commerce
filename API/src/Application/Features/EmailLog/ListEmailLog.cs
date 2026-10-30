using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.EmailLog;

/// <summary>
/// Lists queued/sent emails (newest first) for the admin Email Log, optionally filtered by
/// recipient/subject search, delivery status and/or notification category.
/// </summary>
public record ListEmailLogQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    EmailStatus? Status = null,
    NotificationCategory? Category = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<EmailLogItemDto>>;

public class ListEmailLogQueryHandler : IRequestHandler<ListEmailLogQuery, PaginatedList<EmailLogItemDto>>
{
    // Grid column name -> entity property path. Note "subject" maps to the entity's RenderedSubject
    // (the DTO exposes it as Subject). Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["recipient"] = "RecipientEmail",
        ["subject"] = "RenderedSubject",
        ["templateKey"] = "TemplateKey",
        ["category"] = "Category",
        ["attemptCount"] = "AttemptCount",
        ["createdOnUtc"] = "CreatedOnUtc",
        ["status"] = "Status",
    };

    private readonly IApplicationDbContext _db;

    public ListEmailLogQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<EmailLogItemDto>> Handle(ListEmailLogQuery request, CancellationToken cancellationToken)
    {
        var query = _db.EmailQueue.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(e =>
                e.RecipientEmail.Contains(term)
                || e.RenderedSubject.Contains(term)
                || (e.RecipientName != null && e.RecipientName.Contains(term)));
        }

        if (request.Status.HasValue)
            query = query.Where(e => e.Status == request.Status.Value);

        if (request.Category.HasValue)
            query = query.Where(e => e.Category == request.Category.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap, defaultProperty: "CreatedOnUtc");
        var page = await PaginatedList<EmailQueue>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(EmailLogItemDto.From).ToList();

        return new PaginatedList<EmailLogItemDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
