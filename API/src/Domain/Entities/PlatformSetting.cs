using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A key/value platform setting. Changes are applied on the next request without restart and are
/// audited in <see cref="SettingsChangeHistory"/> (Platform and Technical Foundation blueprint).
/// </summary>
public class PlatformSetting : AuditableEntity
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string"; // string | int | bool | decimal | json
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
}
