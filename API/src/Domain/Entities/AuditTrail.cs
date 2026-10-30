using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Records admin actions and security events (entity type, entity id, action, actor, timestamp)
/// (Database / Logging and Observability blueprints).
/// </summary>
public class AuditTrail : BaseEntity
{
    public Guid? UserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? MetadataJson { get; set; }
    public DateTime TimestampUtc { get; set; }
}
