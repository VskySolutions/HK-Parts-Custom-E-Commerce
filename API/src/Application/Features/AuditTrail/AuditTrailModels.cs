namespace HKPartsEcommerce.Application.Features.AuditTrail;

/// <summary>
/// Read-only projection of an <see cref="HKPartsEcommerce.Domain.Entities.AuditTrail"/> row for the admin audit-trail
/// viewer (WO-61, REQ-ADM-003): who did what to which entity, when, and the request correlation metadata.
/// </summary>
public class AuditTrailDto
{
    public Guid Id { get; set; }

    /// <summary>Id of the admin user who performed the action (null for unauthenticated/system writes).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Human-readable actor label (the admin's email at the time of the action).</summary>
    public string? ActorName { get; set; }

    /// <summary>Action performed: Create / Update / Delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>CLR type name of the affected entity (e.g. "Manufacturer").</summary>
    public string? EntityType { get; set; }

    /// <summary>Primary key of the affected entity, stringified (comma-joined for composite keys).</summary>
    public string? EntityId { get; set; }

    /// <summary>Correlation id of the originating request, for cross-referencing application logs.</summary>
    public string? CorrelationId { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>Optional small JSON payload (e.g. the set of changed property names on an update).</summary>
    public string? MetadataJson { get; set; }

    public DateTime TimestampUtc { get; set; }

    public static AuditTrailDto From(Domain.Entities.AuditTrail a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        ActorName = a.ActorName,
        Action = a.Action,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        CorrelationId = a.CorrelationId,
        IpAddress = a.IpAddress,
        MetadataJson = a.MetadataJson,
        TimestampUtc = a.TimestampUtc,
    };
}
