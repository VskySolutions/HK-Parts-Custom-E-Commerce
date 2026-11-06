namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Storage for <b>private</b> files (customer documents: IDs, licenses). Unlike <see cref="IFileStorage"/>, which
/// returns public URLs, nothing stored here is web-reachable: files are read back only through an authorized
/// API endpoint that streams them. Follows the same "storage.provider" setting (local disk or Azure Blob).
/// </summary>
public interface IPrivateFileStore
{
    /// <summary>Stores the content under <paramref name="folder"/> with a random name; returns the storage key.</summary>
    Task<string> SaveAsync(string folder, string extension, Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file for reading. Throws <c>NotFoundException</c> when it no longer exists.</summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Best-effort delete (never throws).</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
