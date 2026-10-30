using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A centrally-managed uploaded asset (WO-122). Created by the two-step media upload flow; every feature
/// that handles uploaded files references a <see cref="Media"/> row (via its own mapping table) rather
/// than storing raw URLs. Only <see cref="AssetKey"/> ties the row to the storage provider — the public
/// URL is resolved at serve time so it survives a provider change.
/// </summary>
public class Media : AuditableEntity, ISoftDeletable
{
    /// <summary>The name of the file as uploaded, retained for reference.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>URL-safe file name (lowercase, hyphens) used as the storage path component; unique among live media.</summary>
    public string SeoFileName { get; set; } = string.Empty;

    /// <summary>Provider-agnostic storage key returned by the file-storage service.</summary>
    public string AssetKey { get; set; } = string.Empty;

    /// <summary>
    /// Resolved public URL, denormalized so read paths (storefront listings, search) can serve the
    /// image directly without resolving the provider per request. Populated at commit time from the
    /// storage adapter (and, for video embeds / migrated assets, holds the source URL). Refresh via the
    /// storage service if the provider/CDN changes.
    /// </summary>
    public string? Url { get; set; }

    public MediaType MediaType { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    /// <summary>Pixel dimensions for image media (null for non-images or when they could not be read).</summary>
    public int? Width { get; set; }
    public int? Height { get; set; }

    // Editable SEO / accessibility metadata (at upload time and later via PUT).
    public string? AltText { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? Description { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }
}
