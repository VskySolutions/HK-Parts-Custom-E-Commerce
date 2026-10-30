using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.AuditTrail;

/// <summary>
/// Returns a page of audit-trail entries for the admin viewer (WO-61, AC-ADM-003.2), newest first. Filters:
/// a time window (<paramref name="DateFrom"/>/<paramref name="DateTo"/>), the acting admin
/// (<paramref name="UserId"/>), the <paramref name="Action"/> type (Create/Update/Delete), the affected
/// <paramref name="EntityType"/>, and a free-text <paramref name="Search"/> matched against actor, entity
/// type, and entity id. Read-only — the audit trail is never mutated through the API (AC-ADM-003.3).
/// </summary>
public record ListAuditTrailQuery(
    int Page = 1,
    int PageSize = 20,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    Guid? UserId = null,
    string? Action = null,
    string? EntityType = null,
    string? Search = null) : IRequest<PaginatedList<AuditTrailDto>>;

public class ListAuditTrailQueryHandler : IRequestHandler<ListAuditTrailQuery, PaginatedList<AuditTrailDto>>
{
    private readonly IApplicationDbContext _db;

    public ListAuditTrailQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<AuditTrailDto>> Handle(ListAuditTrailQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.AuditTrail> query = _db.AuditTrails.AsNoTracking();

        if (request.DateFrom.HasValue)
            query = query.Where(a => a.TimestampUtc >= request.DateFrom.Value);

        if (request.DateTo.HasValue)
            query = query.Where(a => a.TimestampUtc <= request.DateTo.Value);

        if (request.UserId.HasValue)
            query = query.Where(a => a.UserId == request.UserId.Value);

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a =>
                (a.ActorName != null && a.ActorName.Contains(term)) ||
                (a.EntityType != null && a.EntityType.Contains(term)) ||
                (a.EntityId != null && a.EntityId.Contains(term)));
        }

        // Newest first. An Id tiebreaker gives a total order so OFFSET/FETCH paging stays stable across the
        // many rows a single admin transaction writes with an identical TimestampUtc.
        var ordered = query
            .OrderByDescending(a => a.TimestampUtc)
            .ThenBy(a => a.Id);

        var page = await PaginatedList<Domain.Entities.AuditTrail>.CreateAsync(ordered, request.Page, request.PageSize, cancellationToken);
        var items = page.Items.Select(AuditTrailDto.From).ToList();
        return new PaginatedList<AuditTrailDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}
