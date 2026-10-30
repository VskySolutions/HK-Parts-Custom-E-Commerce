using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Settings;

/// <summary>Admin view of a platform setting. Sensitive values are withheld.</summary>
public class SettingDto
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string ValueType { get; set; } = "string";
    public string? Category { get; set; }
    public string? Description { get; set; }
    public bool IsSensitive { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public static SettingDto From(PlatformSetting s) => new()
    {
        Key = s.Key,
        Value = s.IsSensitive ? null : s.Value,
        ValueType = s.ValueType,
        Category = s.Category,
        Description = s.Description,
        IsSensitive = s.IsSensitive,
        UpdatedAtUtc = s.UpdatedOnUtc,
    };
}

public class SettingHistoryDto
{
    public string SettingKey { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedOnUtc { get; set; }
    public string? ActorName { get; set; }

    public static SettingHistoryDto From(SettingsChangeHistory h) => new()
    {
        SettingKey = h.SettingKey,
        PreviousValue = h.PreviousValue,
        NewValue = h.NewValue,
        ChangedOnUtc = h.ChangedOnUtc,
        ActorName = h.ActorName,
    };
}
