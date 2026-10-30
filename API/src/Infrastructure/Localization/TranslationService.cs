using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Localization;

/// <summary>
/// Resolves translated content with default-language fallback (REQ-STF-004). Per field it prefers the
/// requested language, then the default language; fields translated in neither are omitted so the caller
/// keeps the original value (AC-STF-004.4).
/// </summary>
public class TranslationService : ITranslationService
{
    private readonly IApplicationDbContext _db;

    public TranslationService(IApplicationDbContext db) => _db = db;

    public async Task<string?> GetDefaultLanguageCodeAsync(CancellationToken cancellationToken = default)
        => await _db.Languages.AsNoTracking()
            .Where(l => l.IsDefault)
            .Select(l => l.Code)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string entityType, Guid entityId, IEnumerable<string> fields, string? languageCode, CancellationToken cancellationToken = default)
    {
        var fieldSet = fields.Select(f => f.Trim()).Where(f => f.Length > 0).ToHashSet();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (fieldSet.Count == 0)
            return result;

        var requested = languageCode?.Trim().ToLowerInvariant();
        var defaultCode = await GetDefaultLanguageCodeAsync(cancellationToken);

        // Nothing to do when the requested language is the default (or absent) and there is a default.
        var langCodes = new List<string>();
        if (!string.IsNullOrWhiteSpace(requested)) langCodes.Add(requested);
        if (!string.IsNullOrWhiteSpace(defaultCode) && defaultCode != requested) langCodes.Add(defaultCode!);
        if (langCodes.Count == 0)
            return result;

        var rows = await _db.ContentTranslations.AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == entityId
                        && langCodes.Contains(t.LanguageCode) && fieldSet.Contains(t.FieldName))
            .Select(t => new { t.FieldName, t.LanguageCode, t.Value })
            .ToListAsync(cancellationToken);

        foreach (var field in fieldSet)
        {
            // Prefer the requested language, then the default.
            var value = (requested is not null ? rows.FirstOrDefault(r => r.FieldName == field && r.LanguageCode == requested) : null)
                        ?? rows.FirstOrDefault(r => r.FieldName == field && r.LanguageCode == defaultCode);
            if (value is not null)
                result[field] = value.Value;
        }

        return result;
    }
}
