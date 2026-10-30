using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;

namespace HKPartsEcommerce.Application.Features.Media;

/// <summary>
/// Updates a media row's SEO / accessibility metadata (WO-122) without re-uploading — the asset key and the
/// file itself are unchanged. The SEO file name stays URL-safe and unique.
/// </summary>
public record UpdateMediaCommand(
    Guid Id,
    string SeoFileName,
    string? AltText,
    string? Title,
    string? Caption,
    string? Description) : IRequest<MediaDto>;

public class UpdateMediaCommandHandler : IRequestHandler<UpdateMediaCommand, MediaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public UpdateMediaCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<MediaDto> Handle(UpdateMediaCommand request, CancellationToken cancellationToken)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Media), request.Id);

        var seo = (request.SeoFileName ?? string.Empty).Trim().ToLowerInvariant();
        if (!MediaHelpers.IsValidSeoFileName(seo))
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.SeoFileName),
                    "The SEO file name must be lowercase letters, digits and single hyphens."),
            });

        if (seo != media.SeoFileName
            && await _db.Media.AnyAsync(m => m.Id != media.Id && m.SeoFileName == seo, cancellationToken))
        {
            throw new ConflictException($"Another media item already uses the SEO file name '{seo}'.");
        }

        media.SeoFileName = seo;
        media.AltText = request.AltText;
        media.Title = request.Title;
        media.Caption = request.Caption;
        media.Description = request.Description;

        await _db.SaveChangesAsync(cancellationToken);

        var url = await _storage.GetFileUrlAsync(media.AssetKey, cancellationToken);
        return MediaDto.From(media, url);
    }
}

/// <summary>Soft-deletes a media row and best-effort removes the underlying file (WO-122).</summary>
public record DeleteMediaCommand(Guid Id) : IRequest;

public class DeleteMediaCommandHandler : IRequestHandler<DeleteMediaCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IFileStorage _storage;

    public DeleteMediaCommandHandler(IApplicationDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task Handle(DeleteMediaCommand request, CancellationToken cancellationToken)
    {
        var media = await _db.Media.FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Media), request.Id);

        _db.Media.Remove(media); // soft-deleted by SaveChanges (ISoftDeletable)
        await _db.SaveChangesAsync(cancellationToken);

        // Best-effort physical delete; the DB row is already retired even if this fails.
        try
        {
            await _storage.DeleteFileAsync(media.AssetKey, cancellationToken);
        }
        catch
        {
            // Ignore — orphaned files are reclaimed out-of-band; the record is soft-deleted regardless.
        }
    }
}
