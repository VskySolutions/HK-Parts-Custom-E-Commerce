using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core save interceptor that writes an <see cref="AuditTrail"/> row for every create/update/delete of
/// an auditable entity performed by an admin request (WO-61, AC-ADM-003.1). The rows are added to the same
/// <see cref="DbContext"/> before the save completes, so each audit record commits inside the very same
/// transaction as the change it describes — a failed save writes no audit, and a written audit implies the
/// change persisted.
///
/// <para>Scope: only requests whose path begins with <c>/api/admin</c> are audited. This cleanly excludes
/// storefront/customer/auth endpoints and background jobs (which run with no <see cref="HttpContext"/> at
/// all). Only entities implementing <see cref="IAuditableEntity"/> are considered — which naturally excludes
/// <see cref="AuditTrail"/> itself and <see cref="ApplicationLog"/> (neither implements it), so the audit
/// write can never recurse into auditing its own rows.</para>
/// </summary>
public sealed class AuditTrailInterceptor : SaveChangesInterceptor
{
    /// <summary>Path prefix identifying an admin (state-changing) request. Segment-based, case-insensitive.</summary>
    private const string AdminPathPrefix = "/api/admin";

    /// <summary>
    /// Audit-metadata columns are stamped on every write by <c>AppDbContext.ApplyAuditInformation</c>, so
    /// they are excluded from the "changed properties" summary to keep it focused on business fields.
    /// </summary>
    private static readonly HashSet<string> AuditMetadataProperties = new(StringComparer.Ordinal)
    {
        nameof(IAuditableEntity.CreatedById),
        nameof(IAuditableEntity.CreatedOnUtc),
        nameof(IAuditableEntity.UpdatedById),
        nameof(IAuditableEntity.UpdatedOnUtc),
    };

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuditTrailInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUser = currentUser;
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            AddAuditEntries(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            AddAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext context)
    {
        // Only admin (state-changing) requests are audited. No HttpContext => background job / seeding /
        // non-request save => record nothing. A non-/api/admin request (storefront, customer, auth) is
        // likewise out of scope.
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null ||
            !httpContext.Request.Path.StartsWithSegments(AdminPathPrefix, StringComparison.OrdinalIgnoreCase))
            return;

        var now = _clock.UtcNow;
        var userId = _currentUser.UserId;
        // The principal carries no display name, so the email is the actor label (null for unauthenticated).
        var actorName = _currentUser.Email;
        var correlationId = _currentUser.CorrelationId;
        var ipAddress = _currentUser.IpAddress;

        // Snapshot the auditable entries before adding any AuditTrail rows: AddRange mutates the state
        // manager, and enumerating it while mutating would throw. (AuditTrail is not IAuditableEntity, so it
        // would never re-enter this set anyway.)
        var entries = context.ChangeTracker.Entries<IAuditableEntity>().ToList();

        var audits = new List<AuditTrail>();
        foreach (var entry in entries)
        {
            var action = ResolveAction(entry);
            if (action is null)
                continue;

            audits.Add(new AuditTrail
            {
                UserId = userId,
                ActorName = actorName,
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = ResolveEntityId(entry),
                CorrelationId = correlationId,
                IpAddress = ipAddress,
                MetadataJson = action == ActionUpdate ? BuildChangedPropertiesMetadata(entry) : null,
                TimestampUtc = now,
            });
        }

        if (audits.Count > 0)
            context.AddRange(audits);
    }

    private const string ActionCreate = "Create";
    private const string ActionUpdate = "Update";
    private const string ActionDelete = "Delete";

    /// <summary>
    /// Maps EF change state to an audit action, or null when the entry needs no audit. The DbContext
    /// converts a hard delete of a soft-deletable entity into <c>Modified</c> + <c>Deleted=true</c> before
    /// this interceptor runs, so an entry whose <see cref="ISoftDeletable.Deleted"/> flag just flipped to
    /// true is reported as a <c>Delete</c> rather than an <c>Update</c>.
    /// </summary>
    private static string? ResolveAction(EntityEntry entry) => entry.State switch
    {
        EntityState.Added => ActionCreate,
        EntityState.Deleted => ActionDelete,                                    // hard delete (non-soft-deletable)
        EntityState.Modified => IsSoftDeleteConversion(entry) ? ActionDelete : ActionUpdate,
        _ => null,                                                              // Unchanged / Detached
    };

    /// <summary>
    /// True when this Modified entry is really a soft delete: the entity is soft-deletable and its
    /// <c>Deleted</c> flag was changed from not-true to true in this save.
    /// </summary>
    private static bool IsSoftDeleteConversion(EntityEntry entry)
    {
        if (entry.Entity is not ISoftDeletable)
            return false;

        var deleted = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(ISoftDeletable.Deleted));
        return deleted is { IsModified: true }
            && deleted.CurrentValue is true
            && deleted.OriginalValue is not true;
    }

    /// <summary>Stringifies the primary key (joins components for composite keys); null if none is defined.</summary>
    private static string? ResolveEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
            return null;

        var parts = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(",", parts);
    }

    /// <summary>
    /// Small JSON payload naming the changed business properties on an update (audit-metadata columns
    /// excluded). Returns null when nothing meaningful changed, so the column stays empty rather than noisy.
    /// </summary>
    private static string? BuildChangedPropertiesMetadata(EntityEntry entry)
    {
        var changed = entry.Properties
            .Where(p => p.IsModified && !AuditMetadataProperties.Contains(p.Metadata.Name))
            .Select(p => p.Metadata.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        return changed.Length == 0
            ? null
            : JsonSerializer.Serialize(new { changedProperties = changed });
    }
}
