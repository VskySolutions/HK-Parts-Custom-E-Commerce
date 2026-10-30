using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>A storefront display language the tenant supports (REQ-STF-004). One language is the default.</summary>
public class Language : AuditableEntity
{
    /// <summary>ISO 639-1 code, e.g. "en", "fr".</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NativeName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// A translated value for one field of one entity in one language (REQ-STF-004). Keyed generically by
/// (entity type, entity id, field, language) so any translatable content participates without per-entity
/// tables. Missing translations fall back to the default language (AC-STF-004.4).
/// </summary>
public class ContentTranslation : AuditableEntity
{
    public string EntityType { get; set; } = string.Empty; // e.g. "Product", "Category"
    public Guid EntityId { get; set; }
    public string FieldName { get; set; } = string.Empty;  // e.g. "Name", "FullDescription"
    public string LanguageCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
