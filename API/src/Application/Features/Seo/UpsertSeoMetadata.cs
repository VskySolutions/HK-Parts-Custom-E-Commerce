using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Seo;

/// <summary>
/// Creates or replaces a record's central SEO metadata (one row per entity type + id). Blank strings are
/// stored as null so the storefront falls back to the record's own fields. Invalidates the cached sitemap
/// because <see cref="NoIndex"/> changes which URLs it lists.
/// </summary>
public record UpsertSeoMetadataCommand(
    SeoEntityType EntityType,
    Guid EntityId,
    string? MetaTitle = null,
    string? MetaDescription = null,
    string? MetaKeywords = null,
    string? CanonicalUrl = null,
    string? H1 = null,
    Guid? MetaImageMediaId = null,
    string? MetaOpenGraph = null,
    string? MetaCustomTags = null,
    string? MetaFooterTags = null,
    bool NoIndex = false) : IRequest<SeoMetadataDto>;

public class UpsertSeoMetadataCommandValidator : AbstractValidator<UpsertSeoMetadataCommand>
{
    public UpsertSeoMetadataCommandValidator()
    {
        RuleFor(x => x.EntityType).IsInEnum();
        RuleFor(x => x.EntityId).NotEmpty();
        RuleFor(x => x.MetaTitle).MaximumLength(300);
        RuleFor(x => x.MetaDescription).MaximumLength(500);
        RuleFor(x => x.MetaKeywords).MaximumLength(500);
        RuleFor(x => x.H1).MaximumLength(300);
        RuleFor(x => x.CanonicalUrl).MaximumLength(500)
            .Must(BeAbsoluteHttpUrl!).When(x => !string.IsNullOrWhiteSpace(x.CanonicalUrl))
            .WithMessage("Canonical URL must be an absolute http(s) URL.");
        RuleFor(x => x.MetaOpenGraph).MaximumLength(10000);
        RuleFor(x => x.MetaCustomTags).MaximumLength(10000);
        RuleFor(x => x.MetaFooterTags).MaximumLength(10000);
    }

    private static bool BeAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public class UpsertSeoMetadataCommandHandler : IRequestHandler<UpsertSeoMetadataCommand, SeoMetadataDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISeoService _seo;

    public UpsertSeoMetadataCommandHandler(IApplicationDbContext db, ISeoService seo)
    {
        _db = db;
        _seo = seo;
    }

    public async Task<SeoMetadataDto> Handle(UpsertSeoMetadataCommand request, CancellationToken cancellationToken)
    {
        if (!await SeoEntities.ExistsAsync(_db, request.EntityType, request.EntityId, cancellationToken))
            throw new NotFoundException(request.EntityType.ToString(), request.EntityId);

        if (request.MetaImageMediaId is Guid mediaId
            && !await _db.Media.AnyAsync(m => m.Id == mediaId, cancellationToken))
            throw new NotFoundException(nameof(Media), mediaId);

        var row = await _db.SeoMetadata
            .FirstOrDefaultAsync(s => s.EntityType == request.EntityType && s.EntityId == request.EntityId, cancellationToken);
        if (row is null)
        {
            row = new SeoMetadata { EntityType = request.EntityType, EntityId = request.EntityId };
            _db.SeoMetadata.Add(row);
        }

        row.MetaTitle = Clean(request.MetaTitle);
        row.MetaDescription = Clean(request.MetaDescription);
        row.MetaKeywords = Clean(request.MetaKeywords);
        row.CanonicalUrl = Clean(request.CanonicalUrl);
        row.H1 = Clean(request.H1);
        row.MetaImageMediaId = request.MetaImageMediaId;
        row.MetaOpenGraph = Clean(request.MetaOpenGraph);
        row.MetaCustomTags = Clean(request.MetaCustomTags);
        row.MetaFooterTags = Clean(request.MetaFooterTags);
        row.NoIndex = request.NoIndex;

        await _db.SaveChangesAsync(cancellationToken);
        _seo.InvalidateSitemapCache();

        var saved = await _db.SeoMetadata
            .AsNoTracking()
            .Include(s => s.MetaImage)
            .FirstAsync(s => s.Id == row.Id, cancellationToken);
        return SeoMetadataDto.From(saved);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
