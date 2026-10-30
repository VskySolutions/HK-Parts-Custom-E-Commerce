using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Append-only audit of every settings change (previous value, new value, actor, timestamp).</summary>
public class SettingsChangeHistory : BaseEntity
{
    public string SettingKey { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedOnUtc { get; set; }
    public Guid? ActorId { get; set; }
    public string? ActorName { get; set; }
}
