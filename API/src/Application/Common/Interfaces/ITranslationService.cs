namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Resolves translatable content for a requested language with default-language fallback (REQ-STF-004).
/// Storefront projections call this to overlay translated field values; a field with no translation in
/// either the requested or the default language is omitted so the caller keeps the original value
/// (AC-STF-004.4).
/// </summary>
public interface ITranslationService
{
    /// <summary>The configured default (fallback) language code, or null when none is configured.</summary>
    Task<string?> GetDefaultLanguageCodeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the translated value per requested field for <paramref name="languageCode"/>, falling back
    /// to the default language; fields with no translation are absent from the result.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string entityType, Guid entityId, IEnumerable<string> fields, string? languageCode, CancellationToken cancellationToken = default);
}
