using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Media;

/// <summary>The result of the prepare step: extracted metadata + a suggested SEO name for the user to review.</summary>
public class MediaDraftDto
{
    public string TempId { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string SuggestedSeoFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    /// <summary>Temporary preview URL that streams the prepared (uncommitted) bytes.</summary>
    public string PreviewUrl { get; set; } = string.Empty;
}

/// <summary>The result of the commit step.</summary>
public class MediaCommitResultDto
{
    public Guid MediaId { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
}

/// <summary>A committed media-library row with its resolved public URL.</summary>
public class MediaDto
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string SeoFileName { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? AltText { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? Description { get; set; }
    public string PublicUrl { get; set; } = string.Empty;

    public static MediaDto From(Domain.Entities.Media m, string publicUrl) => new()
    {
        Id = m.Id,
        OriginalFileName = m.OriginalFileName,
        SeoFileName = m.SeoFileName,
        MediaType = m.MediaType.ToString(),
        MimeType = m.MimeType,
        FileSizeBytes = m.FileSizeBytes,
        Width = m.Width,
        Height = m.Height,
        AltText = m.AltText,
        Title = m.Title,
        Caption = m.Caption,
        Description = m.Description,
        PublicUrl = publicUrl,
    };
}
