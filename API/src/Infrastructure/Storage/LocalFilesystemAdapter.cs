using Microsoft.Extensions.Hosting;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>
/// Stores files under a configured root directory on the API host and serves them via the static-file
/// middleware. The default provider (File Storage ADR-002).
/// </summary>
public class LocalFilesystemAdapter : IFileStorageAdapter
{
    private readonly IHostEnvironment _env;
    private readonly ISettingsService _settings;

    public LocalFilesystemAdapter(IHostEnvironment env, ISettingsService settings)
    {
        _env = env;
        _settings = settings;
    }

    public string Name => "LocalFilesystem";

    public async Task<StoredFileReference> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        var folder = StoragePaths.SanitizeFolder(request.FolderPath);
        var fileName = StoragePaths.NewFileName(request.FileName);
        var assetKey = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

        var root = await ResolveRootAsync(cancellationToken);
        var targetDir = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(targetDir);

        var physicalPath = Path.Combine(targetDir, fileName);
        await using (var fileStream = File.Create(physicalPath))
            await request.Content.CopyToAsync(fileStream, cancellationToken);

        return new StoredFileReference(assetKey, await BuildUrlAsync(assetKey, cancellationToken));
    }

    public async Task<string> GetUrlAsync(string assetKey, CancellationToken cancellationToken)
        => await BuildUrlAsync(assetKey, cancellationToken);

    public async Task DeleteAsync(string assetKey, CancellationToken cancellationToken)
    {
        var root = await ResolveRootAsync(cancellationToken);
        var path = Path.Combine(root, assetKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
            File.Delete(path);
    }

    private async Task<string> ResolveRootAsync(CancellationToken ct)
    {
        var configured = await _settings.GetValueAsync("storage.local.root", ct) ?? "wwwroot/uploads";
        return Path.IsPathRooted(configured) ? configured : Path.Combine(_env.ContentRootPath, configured);
    }

    private async Task<string> BuildUrlAsync(string assetKey, CancellationToken ct)
    {
        var cdn = await _settings.GetValueAsync("storage.cdn.base-url", ct);
        if (!string.IsNullOrWhiteSpace(cdn))
            return $"{cdn.TrimEnd('/')}/{assetKey}";

        var requestPath = await _settings.GetValueAsync("storage.local.request-path", ct) ?? "/uploads";
        return $"{requestPath.TrimEnd('/')}/{assetKey}";
    }
}
