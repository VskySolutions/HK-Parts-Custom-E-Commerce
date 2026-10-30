using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Provider-agnostic file storage. The active provider is chosen from settings at runtime; callers
/// never reference a provider directly (File Storage blueprint).
/// </summary>
public interface IFileStorage
{
    Task<StoredFileReference> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default);
    Task<string> GetFileUrlAsync(string assetKey, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string assetKey, CancellationToken cancellationToken = default);
    Task<ConnectivityTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
