using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Localization;

/// <summary>A configured storefront language (admin view).</summary>
public class LanguageDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }

    public static LanguageDto From(Language l) => new()
    {
        Id = l.Id,
        Code = l.Code,
        Name = l.Name,
        NativeName = l.NativeName,
        IsEnabled = l.IsEnabled,
        IsDefault = l.IsDefault,
        DisplayOrder = l.DisplayOrder,
    };
}

/// <summary>An enabled language a buyer may select (public storefront view).</summary>
public class StorefrontLanguageDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsDefault { get; set; }

    public static StorefrontLanguageDto From(Language l) => new()
    {
        Code = l.Code,
        Name = l.Name,
        NativeName = l.NativeName,
        IsDefault = l.IsDefault,
    };
}
