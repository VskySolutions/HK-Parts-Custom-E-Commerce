namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>An upload request. The stream is consumed once by the active storage adapter.</summary>
public class FileUploadRequest
{
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string? ContentType { get; init; }
    public string? FolderPath { get; init; }
}

/// <summary>
/// A stored file. Only <see cref="AssetKey"/> is persisted on entity records; the public URL is
/// resolved at serve time so it survives a provider change (File Storage ADR-001).
/// </summary>
public record StoredFileReference(string AssetKey, string PublicUrl);
