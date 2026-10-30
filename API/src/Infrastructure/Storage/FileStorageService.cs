using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>
/// Provider-agnostic entry point. Resolves the active adapter from the "storage.provider" setting per
/// operation; deletes are best-effort. On upload/probe failure it raises an admin alert and fails fast
/// so files are never silently lost during an outage (File Storage blueprint, AC-TEN-005.5).
/// </summary>
public class FileStorageService : IFileStorage
{
    private readonly IEnumerable<IFileStorageAdapter> _adapters;
    private readonly ISettingsService _settings;
    private readonly IAdminAlertService _alerts;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(
        IEnumerable<IFileStorageAdapter> adapters,
        ISettingsService settings,
        IAdminAlertService alerts,
        ILogger<FileStorageService> logger)
    {
        _adapters = adapters;
        _settings = settings;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task<StoredFileReference> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            return await (await ResolveAdapterAsync(cancellationToken)).UploadAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // Surface the outage to admins and fail fast so the file is never silently lost (AC-TEN-005.5).
            _logger.LogError(ex, "File upload failed via the active storage provider.");
            await RaiseStorageAlertAsync("File upload failed", ex, cancellationToken);
            throw;
        }
    }

    public async Task<string> GetFileUrlAsync(string assetKey, CancellationToken cancellationToken = default)
        => await (await ResolveAdapterAsync(cancellationToken)).GetUrlAsync(assetKey, cancellationToken);

    public async Task DeleteFileAsync(string assetKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await (await ResolveAdapterAsync(cancellationToken)).DeleteAsync(assetKey, cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: a failed delete must not block the parent operation.
            _logger.LogWarning(ex, "Best-effort delete failed for asset {AssetKey}.", assetKey);
        }
    }

    public async Task<ConnectivityTestResult> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        try
        {
            var adapter = await ResolveAdapterAsync(cancellationToken);

            using var probe = new MemoryStream("vsky-storage-probe"u8.ToArray());
            var reference = await adapter.UploadAsync(new FileUploadRequest
            {
                Content = probe,
                FileName = "probe.txt",
                ContentType = "text/plain",
                FolderPath = "_healthcheck",
            }, cancellationToken);

            var url = await adapter.GetUrlAsync(reference.AssetKey, cancellationToken);
            await adapter.DeleteAsync(reference.AssetKey, cancellationToken);

            return new ConnectivityTestResult(true, $"{adapter.Name} write-read-delete probe succeeded ({url}).", now);
        }
        catch (Exception ex)
        {
            await RaiseStorageAlertAsync("Storage connectivity test failed", ex, cancellationToken);
            return new ConnectivityTestResult(false, $"Storage connectivity probe failed: {ex.Message}", now);
        }
    }

    private async Task RaiseStorageAlertAsync(string title, Exception ex, CancellationToken ct)
    {
        try
        {
            await _alerts.RaiseAsync("StorageUnavailable", title, ex.Message, "Error", "FileStorage", ct);
        }
        catch (Exception alertEx)
        {
            // An alert failure must not mask the original storage error.
            _logger.LogWarning(alertEx, "Failed to record a storage-unavailable admin alert.");
        }
    }

    private async Task<IFileStorageAdapter> ResolveAdapterAsync(CancellationToken ct)
    {
        var provider = await _settings.GetValueAsync("storage.provider", ct) ?? "LocalFilesystem";
        return _adapters.FirstOrDefault(a => string.Equals(a.Name, provider, StringComparison.OrdinalIgnoreCase))
            ?? _adapters.First(a => a.Name == "LocalFilesystem");
    }
}
