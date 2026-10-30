using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Logging;

/// <summary>
/// Returns a page of persisted application logs for the admin Log Viewer (WO-70), newest first.
/// Only Error/Fatal severities are surfaced by default (the table is fed by the Error+ Serilog sink);
/// an explicit <paramref name="Level"/> narrows to a single severity. The remaining parameters filter
/// by time window (<paramref name="DateFrom"/>/<paramref name="DateTo"/>), exact
/// <paramref name="CorrelationId"/>, and a <paramref name="Search"/> term matched against the message.
/// </summary>
public record ListApplicationLogsQuery(
    int Page = 1,
    int PageSize = 20,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    string? Level = null,
    string? CorrelationId = null,
    string? Search = null) : IRequest<PaginatedList<ApplicationLogDto>>;

public class ListApplicationLogsQueryHandler : IRequestHandler<ListApplicationLogsQuery, PaginatedList<ApplicationLogDto>>
{
    private readonly IApplicationDbContext _db;

    public ListApplicationLogsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<ApplicationLogDto>> Handle(ListApplicationLogsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ApplicationLog> query = _db.ApplicationLogs.AsNoTracking();

        // Default scope: Error + Fatal only. An explicit Level narrows to that single severity.
        if (!string.IsNullOrWhiteSpace(request.Level))
        {
            var level = request.Level.Trim();
            query = query.Where(l => l.Level == level);
        }
        else
        {
            query = query.Where(l => l.Level == "Error" || l.Level == "Fatal");
        }

        if (request.DateFrom.HasValue)
            query = query.Where(l => l.TimeStamp >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(l => l.TimeStamp <= request.DateTo.Value);

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var correlationId = request.CorrelationId.Trim();
            query = query.Where(l => l.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l => l.Message != null && l.Message.Contains(term));
        }

        var ordered = query.OrderByDescending(l => l.TimeStamp);

        var page = await PaginatedList<ApplicationLog>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(ApplicationLogDto.From).ToList();
        return new PaginatedList<ApplicationLogDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
