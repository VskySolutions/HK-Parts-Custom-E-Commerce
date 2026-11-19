using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Checkout;

/// <summary>Writes the block / unblock history (<see cref="CustomerBlacklistEvent"/>). Staged only; the caller saves.</summary>
public static class CustomerBlacklistHistory
{
    public static void Record(IApplicationDbContext db, CustomerBlacklistEntry entry, CustomerBlacklistAction action, string? reason = null)
    {
        db.CustomerBlacklistEvents.Add(new CustomerBlacklistEvent
        {
            SubjectKey = SubjectKey(entry.Email, entry.CustomerId, entry.FirstName, entry.LastName),
            BlacklistEntryId = entry.Id,
            CustomerId = entry.CustomerId,
            FirstName = entry.FirstName,
            LastName = entry.LastName,
            Email = entry.Email,
            Action = action,
            Reason = reason ?? entry.Reason,
            AtUtc = DateTime.UtcNow,
        });
    }

    /// <summary>Groups one person's events: email, else customer id, else full name (all lower-case).</summary>
    public static string SubjectKey(string? email, Guid? customerId, string? firstName, string? lastName)
    {
        if (!string.IsNullOrWhiteSpace(email)) return email.Trim().ToLowerInvariant();
        if (customerId is Guid id) return $"customer:{id.ToString().ToLowerInvariant()}";
        return $"name:{$"{firstName?.Trim()} {lastName?.Trim()}".Trim().ToLowerInvariant()}";
    }
}

/// <summary>One person in the history tab: how often they were blocked / unblocked and their latest change.</summary>
public class CustomerBlacklistHistorySummaryDto
{
    public string SubjectKey { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public int TimesBlocked { get; set; }
    public int TimesUnblocked { get; set; }
    public string LastAction { get; set; } = string.Empty;
    public DateTime LastActionUtc { get; set; }
}

public class CustomerBlacklistHistoryEventDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime AtUtc { get; set; }
    public string? ChangedBy { get; set; }
}

public record ListCustomerBlacklistHistoryQuery(string? Search = null, int Page = 1, int PageSize = 25)
    : IRequest<PaginatedList<CustomerBlacklistHistorySummaryDto>>;

public class ListCustomerBlacklistHistoryQueryHandler
    : IRequestHandler<ListCustomerBlacklistHistoryQuery, PaginatedList<CustomerBlacklistHistorySummaryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCustomerBlacklistHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<CustomerBlacklistHistorySummaryDto>> Handle(ListCustomerBlacklistHistoryQuery request, CancellationToken ct)
    {
        var events = _db.CustomerBlacklistEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            var keys = events.Where(e => (e.Email != null && e.Email.Contains(s))
                                         || (e.FirstName != null && e.FirstName.Contains(s))
                                         || (e.LastName != null && e.LastName.Contains(s)))
                .Select(e => e.SubjectKey);
            events = events.Where(e => keys.Contains(e.SubjectKey));
        }

        var grouped = events
            .GroupBy(e => e.SubjectKey)
            .Select(g => new HistoryAggregate
            {
                SubjectKey = g.Key,
                TimesBlocked = g.Count(e => e.Action == CustomerBlacklistAction.Blocked),
                TimesUnblocked = g.Count(e => e.Action == CustomerBlacklistAction.Unblocked),
                LastActionUtc = g.Max(e => e.AtUtc),
            })
            .OrderByDescending(x => x.LastActionUtc);

        var page = await PaginatedList<HistoryAggregate>.CreateAsync(grouped, request.Page, Math.Clamp(request.PageSize, 1, 200), ct);

        // Name / email / latest action from each person's most recent event.
        var keysOnPage = page.Items.Select(x => x.SubjectKey).ToList();
        var latest = (await _db.CustomerBlacklistEvents.AsNoTracking()
                .Where(e => keysOnPage.Contains(e.SubjectKey))
                .OrderByDescending(e => e.AtUtc).ThenByDescending(e => e.CreatedOnUtc)
                .Select(e => new { e.SubjectKey, e.CustomerId, e.FirstName, e.LastName, e.Email, e.Action })
                .ToListAsync(ct))
            .GroupBy(e => e.SubjectKey)
            .ToDictionary(g => g.Key, g => g.First());

        var items = page.Items.Select(x =>
        {
            latest.TryGetValue(x.SubjectKey, out var last);
            return new CustomerBlacklistHistorySummaryDto
            {
                SubjectKey = x.SubjectKey,
                CustomerId = last?.CustomerId,
                FirstName = last?.FirstName,
                LastName = last?.LastName,
                Email = last?.Email,
                TimesBlocked = x.TimesBlocked,
                TimesUnblocked = x.TimesUnblocked,
                LastAction = last?.Action.ToString() ?? string.Empty,
                LastActionUtc = x.LastActionUtc,
            };
        }).ToList();

        return new PaginatedList<CustomerBlacklistHistorySummaryDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public class HistoryAggregate
    {
        public string SubjectKey { get; set; } = string.Empty;
        public int TimesBlocked { get; set; }
        public int TimesUnblocked { get; set; }
        public DateTime LastActionUtc { get; set; }
    }
}

/// <summary>Every block / unblock of one person (by <see cref="CustomerBlacklistEvent.SubjectKey"/>), newest first.</summary>
public record ListCustomerBlacklistHistoryEventsQuery(string SubjectKey) : IRequest<List<CustomerBlacklistHistoryEventDto>>;

public class ListCustomerBlacklistHistoryEventsQueryHandler
    : IRequestHandler<ListCustomerBlacklistHistoryEventsQuery, List<CustomerBlacklistHistoryEventDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCustomerBlacklistHistoryEventsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<CustomerBlacklistHistoryEventDto>> Handle(ListCustomerBlacklistHistoryEventsQuery request, CancellationToken ct)
    {
        var key = (request.SubjectKey ?? string.Empty).Trim();
        if (key.Length == 0) return new List<CustomerBlacklistHistoryEventDto>();

        var rows = await _db.CustomerBlacklistEvents.AsNoTracking()
            .Where(e => e.SubjectKey == key)
            .OrderByDescending(e => e.AtUtc).ThenByDescending(e => e.CreatedOnUtc)
            .Select(e => new
            {
                e.Id,
                e.Action,
                e.Reason,
                e.AtUtc,
                ChangedBy = _db.Users.IgnoreQueryFilters().Where(u => u.Id == e.CreatedById).Select(u => u.Username).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(r => new CustomerBlacklistHistoryEventDto
        {
            Id = r.Id,
            Action = r.Action.ToString(),
            Reason = r.Reason,
            AtUtc = r.AtUtc,
            ChangedBy = r.ChangedBy,
        }).ToList();
    }
}
