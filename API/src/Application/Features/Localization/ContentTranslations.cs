using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Localization;

/// <summary>One translated field value.</summary>
public class ContentTranslationDto
{
    public string FieldName { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;

    public static ContentTranslationDto From(ContentTranslation t) => new()
    {
        FieldName = t.FieldName,
        LanguageCode = t.LanguageCode,
        Value = t.Value,
    };
}

/// <summary>Returns all translations authored for one entity (admin translation editor).</summary>
public record GetContentTranslationsQuery(string EntityType, Guid EntityId) : IRequest<List<ContentTranslationDto>>;

public class GetContentTranslationsQueryHandler : IRequestHandler<GetContentTranslationsQuery, List<ContentTranslationDto>>
{
    private readonly IApplicationDbContext _db;
    public GetContentTranslationsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ContentTranslationDto>> Handle(GetContentTranslationsQuery request, CancellationToken cancellationToken)
    {
        var entityType = request.EntityType.Trim();
        var rows = await _db.ContentTranslations.AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == request.EntityId)
            .ToListAsync(cancellationToken);
        return rows.Select(ContentTranslationDto.From).ToList();
    }
}

/// <summary>
/// Upserts the translations for one entity in one language (AC-STF-004.3). A blank value removes that
/// field's translation (falling back to the default language).
/// </summary>
public record SetContentTranslationsCommand(string EntityType, Guid EntityId, string LanguageCode, Dictionary<string, string?> Values)
    : IRequest<List<ContentTranslationDto>>;

public class SetContentTranslationsCommandValidator : AbstractValidator<SetContentTranslationsCommand>
{
    public SetContentTranslationsCommandValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(64);
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(16);
    }
}

public class SetContentTranslationsCommandHandler : IRequestHandler<SetContentTranslationsCommand, List<ContentTranslationDto>>
{
    private readonly IApplicationDbContext _db;
    public SetContentTranslationsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<ContentTranslationDto>> Handle(SetContentTranslationsCommand request, CancellationToken cancellationToken)
    {
        var entityType = request.EntityType.Trim();
        var lang = request.LanguageCode.Trim().ToLowerInvariant();

        var existing = await _db.ContentTranslations
            .Where(t => t.EntityType == entityType && t.EntityId == request.EntityId && t.LanguageCode == lang)
            .ToListAsync(cancellationToken);

        foreach (var (field, value) in request.Values)
        {
            var fieldName = field.Trim();
            if (fieldName.Length == 0)
                continue;

            var row = existing.FirstOrDefault(t => t.FieldName == fieldName);
            if (string.IsNullOrWhiteSpace(value))
            {
                if (row is not null)
                    _db.ContentTranslations.Remove(row);
                continue;
            }

            if (row is null)
                _db.ContentTranslations.Add(new ContentTranslation
                {
                    EntityType = entityType,
                    EntityId = request.EntityId,
                    FieldName = fieldName,
                    LanguageCode = lang,
                    Value = value,
                });
            else
                row.Value = value;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var updated = await _db.ContentTranslations.AsNoTracking()
            .Where(t => t.EntityType == entityType && t.EntityId == request.EntityId && t.LanguageCode == lang)
            .ToListAsync(cancellationToken);
        return updated.Select(ContentTranslationDto.From).ToList();
    }
}
