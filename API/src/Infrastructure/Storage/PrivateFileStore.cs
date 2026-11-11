using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>
/// Private (non-web-reachable) file store, selected by the same "storage.provider" setting as public storage:
/// <list type="bullet">
/// <item><b>LocalFilesystem</b>: under "storage.private.root" (default <c>App_Data/private</c> in the content root),
/// which is outside the static-file uploads root. Refuses to run if configured inside it.</item>
/// <item><b>AzureBlobStorage</b>: container "storage.azure.private-container" (default <c>private</c>) created with
/// <see cref="PublicAccessType.None"/>.</item>
/// </list>
/// Stored names are random GUIDs; keys are re-validated on every read/delete so a key can never escape the root.
/// </summary>
public class PrivateFileStore : IPrivateFileStore
{
    private readonly IHostEnvironment _env;
    private readonly ISettingsService _settings;
    private readonly ICredentialVault _vault;
    private readonly ILogger<PrivateFileStore> _logger;

    public PrivateFileStore(IHostEnvironment env, ISettingsService settings, ICredentialVault vault, ILogger<PrivateFileStore> logger)
    {
        _env = env;
        _settings = settings;
        _vault = vault;
        _logger = logger;
    }

    public async Task<string> SaveAsync(string folder, string extension, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var cleanFolder = StoragePaths.SanitizeFolder(folder);
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        var key = $"{cleanFolder}/{Guid.NewGuid():N}{ext.ToLowerInvariant()}".TrimStart('/');

        if (await IsAzureAsync(cancellationToken))
        {
            var blob = (await GetContainerAsync(cancellationToken)).GetBlobClient(key);
            await blob.UploadAsync(content, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, cancellationToken);
            return key;
        }

        var path = await ResolveLocalPathAsync(key, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(file, cancellationToken);
        return key;
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default)
    {
        if (await IsAzureAsync(cancellationToken))
        {
            var blob = (await GetContainerAsync(cancellationToken)).GetBlobClient(ValidateKey(key));
            if (!await blob.ExistsAsync(cancellationToken))
                throw new NotFoundException("The stored file could not be found.");
            return await blob.OpenReadAsync(cancellationToken: cancellationToken);
        }

        var path = await ResolveLocalPathAsync(key, cancellationToken);
        if (!File.Exists(path))
            throw new NotFoundException("The stored file could not be found.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await IsAzureAsync(cancellationToken))
            {
                await (await GetContainerAsync(cancellationToken)).GetBlobClient(ValidateKey(key)).DeleteIfExistsAsync(cancellationToken: cancellationToken);
                return;
            }

            var path = await ResolveLocalPathAsync(key, cancellationToken);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex)
        {
            // Best-effort, like IFileStorage deletes: never block the parent operation.
            _logger.LogWarning(ex, "Best-effort delete failed for private file {Key}.", key);
        }
    }

    private async Task<bool> IsAzureAsync(CancellationToken ct)
        => string.Equals(await _settings.GetValueAsync("storage.provider", ct), "AzureBlobStorage", StringComparison.OrdinalIgnoreCase);

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        var connectionString = await _vault.GetCredentialAsync("azure-blob", ct)
            ?? throw new InvalidOperationException("Azure Blob connection string is not configured in the Credential Vault (service type 'azure-blob').");
        var name = await _settings.GetValueAsync("storage.azure.private-container", ct) ?? "private";
        var container = new BlobServiceClient(connectionString).GetBlobContainerClient(name);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
        return container;
    }

    /// <summary>Maps a key to a physical path, guaranteeing it stays inside the private root (and that the root is not public).</summary>
    private async Task<string> ResolveLocalPathAsync(string key, CancellationToken ct)
    {
        var root = Path.GetFullPath(await ResolveRootAsync("storage.private.root", "App_Data/private", ct));
        var publicRoot = Path.GetFullPath(await ResolveRootAsync("storage.local.root", "wwwroot/uploads", ct));
        if (IsUnder(root, publicRoot) || IsUnder(publicRoot, root))
            throw new InvalidOperationException("storage.private.root must not overlap the public uploads root (storage.local.root).");

        var full = Path.GetFullPath(Path.Combine(root, ValidateKey(key).Replace('/', Path.DirectorySeparatorChar)));
        if (!IsUnder(full, root))
            throw new NotFoundException("The stored file could not be found.");
        return full;
    }

    private async Task<string> ResolveRootAsync(string setting, string fallback, CancellationToken ct)
    {
        var configured = await _settings.GetValueAsync(setting, ct) ?? fallback;
        return Path.IsPathRooted(configured) ? configured : Path.Combine(_env.ContentRootPath, configured);
    }

    private static string ValidateKey(string key)
    {
        var clean = StoragePaths.SanitizeFolder(key);
        if (string.IsNullOrEmpty(clean) || clean != key.Replace('\\', '/').Trim('/'))
            throw new NotFoundException("The stored file could not be found.");
        return clean;
    }

    private static bool IsUnder(string path, string root)
    {
        var r = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(r, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
