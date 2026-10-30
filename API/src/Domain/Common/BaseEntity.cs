namespace HKPartsEcommerce.Domain.Common;

/// <summary>Base type for all persisted entities with a Guid surrogate key.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>Marks an entity that tracks creation/modification actor and timestamps.</summary>
public interface IAuditableEntity
{
    Guid? CreatedById { get; set; }
    DateTime CreatedOnUtc { get; set; }
    Guid? UpdatedById { get; set; }
    DateTime UpdatedOnUtc { get; set; }
}

/// <summary>Base type for entities that carry audit metadata, populated by the DbContext.</summary>
public abstract class AuditableEntity : BaseEntity, IAuditableEntity
{
    public Guid? CreatedById { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public Guid? UpdatedById { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}

/// <summary>Marks an entity that is soft-deleted (never physically removed) via a global query filter.</summary>
public interface ISoftDeletable
{
    bool Deleted { get; set; }
    DateTime? DeletedOnUtc { get; set; }
}
