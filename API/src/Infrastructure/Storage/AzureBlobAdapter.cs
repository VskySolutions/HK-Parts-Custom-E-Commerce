using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.Storage;

/// <summary>
/// Stores files in an Azure Blob container. The connection string is resolved at runtime from the
/// Credential Vault (service type "azure-blob") and never held beyond a single operation.
/// </summary>
public class AzureBlobAdapter : IFileStorageAdapter
{
    private readonly ICredentialVault _vault;
    private readonly ISettingsService _settings;

    public AzureBlobAdapter(ICredentialVault vault, ISettingsService settings)
    {
        _vault = vault;
        _settings = settings;
    }

    public string Name => "AzureBlobStorage";

    public async Task<StoredFileReference> UploadAsync(FileUploadRequest request, CancellationToken cancellationToken)
    {
        var folder = StoragePaths.SanitizeFolder(request.FolderPath);
        var fileName = StoragePaths.NewFileName(request.FileName);
        var blobName = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";

        var container = await GetContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(blobName);

        var options = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = request.ContentType ?? "application/octet-stream" },
        };
        await blob.UploadAsync(request.Content, options, cancellationToken);

        return new StoredFileReference(blobName, await BuildUrlAsync(blob, blobName, cancellationToken));
    }

    public async Task<string> GetUrlAsync(string assetKey, CancellationToken cancellationToken)
    {
        var cdn = await _settings.GetValueAsync("storage.cdn.base-url", cancellationToken);
        if (!string.IsNullOrWhiteSpace(cdn))
            return $"{cdn.TrimEnd('/')}/{assetKey}";

        var container = await GetContainerAsync(cancellationToken);
        return container.GetBlobClient(assetKey).Uri.ToString();
    }

    public async Task DeleteAsync(string assetKey, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(cancellationToken);
        await container.GetBlobClient(assetKey).DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        var connectionString = await _vault.GetCredentialAsync("azure-blob", ct)
            ?? throw new InvalidOperationException(
                "Azure Blob connection string is not configured in the Credential Vault (service type 'azure-blob').");

        var containerName = await _settings.GetValueAsync("storage.azure.container", ct) ?? "uploads";
        var container = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        return container;
    }

    private async Task<string> BuildUrlAsync(BlobClient blob, string blobName, CancellationToken ct)
    {
        var cdn = await _settings.GetValueAsync("storage.cdn.base-url", ct);
        return string.IsNullOrWhiteSpace(cdn) ? blob.Uri.ToString() : $"{cdn.TrimEnd('/')}/{blobName}";
    }
}
