using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// A prepared-but-not-committed upload held between the two-step media flow's prepare and commit calls
/// (WO-122). Carries the file bytes plus the metadata extracted at prepare time.
/// </summary>
public record MediaTempEntry(
    byte[] Content,
    string OriginalFileName,
    string MimeType,
    MediaType MediaType,
    long FileSizeBytes,
    int? Width,
    int? Height);

/// <summary>
/// Short-lived store for prepared media uploads, keyed by an opaque temp id that expires after a TTL
/// (default 10 minutes). Backed by the in-process memory cache.
/// </summary>
public interface IMediaTempStore
{
    /// <summary>Stores a prepared entry and returns its temp id.</summary>
    string Store(MediaTempEntry entry);

    /// <summary>Retrieves a prepared entry, or null when the temp id is unknown or expired.</summary>
    MediaTempEntry? Get(string tempId);

    /// <summary>Removes a prepared entry (after a successful commit).</summary>
    void Remove(string tempId);
}
