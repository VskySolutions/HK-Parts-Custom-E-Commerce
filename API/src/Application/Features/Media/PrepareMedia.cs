using FluentValidation.Results;
using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Media;

/// <summary>
/// Step 1 of the two-step upload (WO-122): processes the file in memory (no DB write), extracts MIME,
/// size and image dimensions, suggests an SEO file name, and parks the bytes in the temp store under a
/// short-lived <c>tempId</c>. Enforces the DB-backed maximum file size.
/// </summary>
public record PrepareMediaCommand(byte[] Content, string OriginalFileName, string? ContentType)
    : IRequest<MediaDraftDto>;

public class PrepareMediaCommandHandler : IRequestHandler<PrepareMediaCommand, MediaDraftDto>
{
    // Setting key + fallback for the maximum upload size (50 MB).
    private const string MaxSizeSettingKey = "media.max-file-size-bytes";
    private const long DefaultMaxSizeBytes = 52_428_800;

    private readonly IMediaTempStore _temp;
    private readonly ISettingsService _settings;

    public PrepareMediaCommandHandler(IMediaTempStore temp, ISettingsService settings)
    {
        _temp = temp;
        _settings = settings;
    }

    public async Task<MediaDraftDto> Handle(PrepareMediaCommand request, CancellationToken cancellationToken)
    {
        if (request.Content is null || request.Content.Length == 0)
            throw new ValidationException(new[] { new ValidationFailure("file", "A non-empty file is required.") });

        var maxBytes = MediaHelpers.ParseBytes(
            await _settings.GetValueAsync(MaxSizeSettingKey, cancellationToken), DefaultMaxSizeBytes);

        if (request.Content.LongLength > maxBytes)
        {
            var maxMb = Math.Round(maxBytes / 1_048_576d, 1);
            throw new ValidationException(new[]
            {
                new ValidationFailure("file", $"The file exceeds the maximum upload size of {maxMb} MB."),
            });
        }

        var mimeType = string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType.Trim();
        var mediaType = MediaHelpers.ClassifyMediaType(mimeType);

        int? width = null, height = null;
        if (mediaType == MediaType.Image && MediaHelpers.TryReadImageDimensions(request.Content, out var w, out var h))
        {
            width = w;
            height = h;
        }

        var tempId = _temp.Store(new MediaTempEntry(
            request.Content, request.OriginalFileName, mimeType, mediaType, request.Content.LongLength, width, height));

        return new MediaDraftDto
        {
            TempId = tempId,
            OriginalFileName = request.OriginalFileName,
            SuggestedSeoFileName = MediaHelpers.ToSeoFileName(request.OriginalFileName),
            MediaType = mediaType.ToString(),
            MimeType = mimeType,
            FileSizeBytes = request.Content.LongLength,
            Width = width,
            Height = height,
            PreviewUrl = $"/api/admin/media/temp/{tempId}",
        };
    }
}

/// <summary>Streams the prepared (uncommitted) bytes so the admin can preview before committing.</summary>
public record GetMediaPreviewQuery(string TempId) : IRequest<MediaPreviewDto?>;

public class MediaPreviewDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
}

public class GetMediaPreviewQueryHandler : IRequestHandler<GetMediaPreviewQuery, MediaPreviewDto?>
{
    private readonly IMediaTempStore _temp;

    public GetMediaPreviewQueryHandler(IMediaTempStore temp) => _temp = temp;

    public Task<MediaPreviewDto?> Handle(GetMediaPreviewQuery request, CancellationToken cancellationToken)
    {
        var entry = _temp.Get(request.TempId);
        return Task.FromResult(entry is null
            ? null
            : new MediaPreviewDto { Content = entry.Content, ContentType = entry.MimeType });
    }
}
