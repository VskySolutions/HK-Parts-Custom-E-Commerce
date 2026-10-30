using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CmsSearchContent;

/// <summary>
/// Upserts the singleton storefront search-page content (WO-105): creates the row when none exists, otherwise
/// updates it in place. Any text field left blank is stored as null so it transparently falls back to the
/// in-code default at read time. A chosen no-results banner/collection must exist.
/// </summary>
public record UpdateSearchPageContentCommand(
    string? Heading = null,
    string? PlaceholderText = null,
    string? ResultsCountLabel = null,
    string? NoResultsMessage = null,
    Guid? NoResultsBannerId = null,
    Guid? NoResultsCollectionId = null) : IRequest<CmsSearchPageContentDto>;

public class UpdateSearchPageContentCommandValidator : AbstractValidator<UpdateSearchPageContentCommand>
{
    public UpdateSearchPageContentCommandValidator()
    {
        // Mirror the DB column lengths (Heading/PlaceholderText/ResultsCountLabel are nvarchar(300)).
        RuleFor(x => x.Heading).MaximumLength(300);
        RuleFor(x => x.PlaceholderText).MaximumLength(300);
        RuleFor(x => x.ResultsCountLabel).MaximumLength(300);
        // NoResultsMessage is rich HTML stored as nvarchar(max) — left uncapped.
    }
}

public class UpdateSearchPageContentCommandHandler : IRequestHandler<UpdateSearchPageContentCommand, CmsSearchPageContentDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateSearchPageContentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CmsSearchPageContentDto> Handle(UpdateSearchPageContentCommand request, CancellationToken cancellationToken)
    {
        // The optional references must resolve to existing (non-deleted) records so the storefront read never
        // points at a missing banner/collection. AnyAsync honours each entity's soft-delete query filter.
        if (request.NoResultsBannerId is Guid bannerId
            && !await _db.CMSBanners.AsNoTracking().AnyAsync(b => b.Id == bannerId, cancellationToken))
            throw new NotFoundException(nameof(CMSBanner), bannerId);

        if (request.NoResultsCollectionId is Guid collectionId
            && !await _db.CMSProductCollections.AsNoTracking().AnyAsync(c => c.Id == collectionId, cancellationToken))
            throw new NotFoundException(nameof(CMSProductCollection), collectionId);

        // Singleton config: load the one row (tracked) or create it.
        var entity = await _db.CMSSearchPageContents
            .OrderBy(x => x.CreatedOnUtc)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new CMSSearchPageContent();
            _db.CMSSearchPageContents.Add(entity);
        }

        // Blank → null so a cleared field falls back to the in-code default at read time.
        entity.Heading = Normalize(request.Heading);
        entity.PlaceholderText = Normalize(request.PlaceholderText);
        entity.ResultsCountLabel = Normalize(request.ResultsCountLabel);
        entity.NoResultsMessage = Normalize(request.NoResultsMessage);
        entity.NoResultsBannerId = request.NoResultsBannerId;
        entity.NoResultsCollectionId = request.NoResultsCollectionId;

        await _db.SaveChangesAsync(cancellationToken);
        return CmsSearchPageContentDto.From(entity);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
