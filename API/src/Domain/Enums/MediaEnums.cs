namespace HKPartsEcommerce.Domain.Enums;

/// <summary>
/// Broad category of an uploaded <see cref="Entities.Media"/> asset, derived from its MIME type. Drives
/// allowed-type validation and media-library filtering (WO-122).
/// </summary>
public enum MediaType
{
    Image = 0,
    Video = 1,
    Document = 2,
    Audio = 3,
    Other = 4,
}
