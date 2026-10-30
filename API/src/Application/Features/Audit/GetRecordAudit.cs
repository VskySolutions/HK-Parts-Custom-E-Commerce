using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Application.Features.Audit;

/// <summary>
/// Loads the creation/modification audit metadata for any registered admin entity, resolving the actor
/// ids to usernames. Backs the standard record-audit footer; the caller (controller) is responsible for
/// authorizing access against the entity's owning module.
/// </summary>
public record GetRecordAuditQuery(string EntityType, Guid Id) : IRequest<RecordAuditDto>;

public class GetRecordAuditQueryHandler : IRequestHandler<GetRecordAuditQuery, RecordAuditDto>
{
    private readonly IApplicationDbContext _db;

    public GetRecordAuditQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<RecordAuditDto> Handle(GetRecordAuditQuery request, CancellationToken cancellationToken)
    {
        if (!RecordAuditRegistry.TryGet(request.EntityType, out var type, out _))
            throw new NotFoundException("Record type", request.EntityType);

        // FindAsync loads by primary key across any mapped type (and, unlike a LINQ query, ignores the
        // soft-delete filter — desirable here: audit info should remain visible for archived records).
        if (await _db.FindAsync(type, new object[] { request.Id }, cancellationToken) is not IAuditableEntity audit)
            throw new NotFoundException(request.EntityType, request.Id);

        var actorIds = new[] { audit.CreatedById, audit.UpdatedById }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var names = actorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, u.Email })
                .ToDictionaryAsync(
                    u => u.Id,
                    u => string.IsNullOrWhiteSpace(u.Username) ? u.Email : u.Username,
                    cancellationToken);

        string? NameFor(Guid? id) => id.HasValue && names.TryGetValue(id.Value, out var name) ? name : null;

        return new RecordAuditDto
        {
            CreatedById = audit.CreatedById,
            CreatedBy = NameFor(audit.CreatedById),
            CreatedOnUtc = audit.CreatedOnUtc,
            UpdatedById = audit.UpdatedById,
            UpdatedBy = NameFor(audit.UpdatedById),
            UpdatedOnUtc = audit.UpdatedOnUtc,
        };
    }
}
