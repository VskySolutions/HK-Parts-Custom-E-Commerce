using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.TaxExemptionRequests;

/// <summary>The stored document, returned to the customer portal so it can show and submit it.</summary>
public class TaxExemptionDocumentUploadDto
{
    public Guid MediaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Uploads one supporting tax document for the signed-in customer and stores it in the central Media table
/// (AC-TAX-003.2).
/// <para>
/// This is a deliberately separate, single-shot command rather than a reuse of the admin
/// prepare→commit pair: the admin flow is gated by <c>RequireModule(Storage)</c> which storefront customers
/// can never satisfy, its temp store is an in-process memory cache (a prepare and commit could land on
/// different instances), and its preview URL points at an admin-only endpoint. It also applies a much
/// tighter policy than the admin uploader — a small size cap and a strict MIME allow-list — because this is
/// the only upload surface reachable by a non-staff user.
/// </para>
/// </summary>
public record UploadTaxExemptionDocumentCommand(byte[] Content, string OriginalFileName, string? ContentType)
    : IRequest<TaxExemptionDocumentUploadDto>;

public class UploadTaxExemptionDocumentCommandHandler
    : IRequestHandler<UploadTaxExemptionDocumentCommand, TaxExemptionDocumentUploadDto>
{
    /// <summary>Certificates are documents or photos of one — nothing else has a reason to be here.</summary>
    private static readonly IReadOnlyDictionary<string, string> AllowedMimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = ".pdf",
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp",
        };

    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IFileStorage _storage;

    public UploadTaxExemptionDocumentCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IFileStorage storage)
    {
        _db = db;
        _current = current;
        _storage = storage;
    }

    public async Task<TaxExemptionDocumentUploadDto> Handle(
        UploadTaxExemptionDocumentCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is not Guid userId)
            throw new UnauthorizedException("Authentication is required to upload a tax document.");

        if (!await _db.Customers.AsNoTracking().AnyAsync(c => c.UserId == userId, cancellationToken))
            throw new ForbiddenAccessException("The current user does not have a customer profile.");

        if (request.Content is null || request.Content.Length == 0)
            throw new ValidationException(new[] { new ValidationFailure("file", "A non-empty file is required.") });

        if (request.Content.LongLength > MaxSizeBytes)
            throw new ValidationException(new[]
            {
                new ValidationFailure("file", $"The file exceeds the maximum upload size of {MaxSizeBytes / 1_048_576} MB."),
            });

        var mimeType = (request.ContentType ?? string.Empty).Trim();
        if (!AllowedMimeTypes.TryGetValue(mimeType, out var extension))
            throw new ValidationException(new[]
            {
                new ValidationFailure("file", "Only PDF, JPG, PNG and WEBP files are accepted."),
            });

        // An unguessable stored name: a tax certificate is sensitive, and committed media are served from a
        // public URL, so the file name must not be derivable from the customer's original one.
        var seoFileName = $"tax-document-{Guid.NewGuid():n}";
        var mediaType = MediaHelpers.ClassifyMediaType(mimeType);

        StoredFileReference reference;
        await using (var stream = new MemoryStream(request.Content, writable: false))
        {
            reference = await _storage.UploadAsync(new FileUploadRequest
            {
                Content = stream,
                FileName = seoFileName + extension,
                ContentType = mimeType,
                FolderPath = "tax-documents",
            }, cancellationToken);
        }

        var media = new Domain.Entities.Media
        {
            OriginalFileName = request.OriginalFileName,
            SeoFileName = seoFileName,
            AssetKey = reference.AssetKey,
            Url = reference.PublicUrl,
            MediaType = mediaType,
            MimeType = mimeType,
            FileSizeBytes = request.Content.LongLength,
        };

        // CreatedById is stamped from the JWT by the DbContext — that is what proves ownership when the
        // customer later attaches this media id to their request.
        _db.Media.Add(media);
        await _db.SaveChangesAsync(cancellationToken);

        return new TaxExemptionDocumentUploadDto
        {
            MediaId = media.Id,
            FileName = request.OriginalFileName,
            Url = reference.PublicUrl,
        };
    }
}
