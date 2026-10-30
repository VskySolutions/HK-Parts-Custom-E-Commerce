using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>A concrete storage backend (local filesystem or Azure Blob). Selected by <see cref="Name"/>.</summary>
public interface IFileStorageAdapter
{
    /// <summary>Matches the "storage.provider" setting value.</summary>
    string Name { get; }

    Task<StoredFileReference> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken);
    Task<string> GetUrlAsync(string assetKey, CancellationToken cancellationToken);
    Task DeleteAsync(string assetKey, CancellationToken cancellationToken);
}

/// <summary>Shared helpers for storage adapters.</summary>
internal static class StoragePaths
{
    /// <summary>Normalizes a logical folder path, stripping traversal segments.</summary>
    public static string SanitizeFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return string.Empty;

        var parts = folder.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p != "." && p != "..");

        return string.Join('/', parts);
    }

    public static string NewFileName(string originalFileName)
    {
        var ext = Path.GetExtension(originalFileName);
        return $"{Guid.NewGuid():N}{ext}";
    }
}
