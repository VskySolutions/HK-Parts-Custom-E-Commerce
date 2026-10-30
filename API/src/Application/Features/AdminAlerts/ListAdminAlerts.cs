using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.AdminAlerts;

/// <summary>Paged admin-alert history (newest first), filterable by free-text search, severity and resolved state.</summary>
public record ListAdminAlertsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Severity = null,
    bool? Resolved = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PaginatedList<AdminAlertDto>>;

public class ListAdminAlertsQueryHandler
    : IRequestHandler<ListAdminAlertsQuery, PaginatedList<AdminAlertDto>>
{
    // Grid column name -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["severity"] = "Severity",
        ["alertType"] = "AlertType",
        ["title"] = "Title",
        ["source"] = "Source",
        ["createdOnUtc"] = "CreatedOnUtc",
        ["status"] = "IsResolved",
    };

    private readonly IApplicationDbContext _db;

    public ListAdminAlertsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<AdminAlertDto>> Handle(ListAdminAlertsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.AdminAlerts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a =>
                a.Title.Contains(term)
                || a.AlertType.Contains(term)
                || (a.Message != null && a.Message.Contains(term))
                || (a.Source != null && a.Source.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Severity))
            query = query.Where(a => a.Severity == request.Severity);

        if (request.Resolved.HasValue)
            query = query.Where(a => a.IsResolved == request.Resolved.Value);

        var ordered = query.ApplySort(request.SortBy, request.SortDescending, SortMap, defaultProperty: "CreatedOnUtc");
        var page = await PaginatedList<AdminAlert>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(AdminAlertDto.From).ToList();
        return new PaginatedList<AdminAlertDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
