using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// One licensed dealer in the FFL directory (FFL shipping requirements, section 6/8 — legacy
/// <c>ffl_database</c> plus the store-maintained <c>dealer_data</c> row, merged). The ATF columns are
/// refreshed by the admin import; the dealer-data columns (<see cref="AllowsTransfers"/>,
/// <see cref="LastTransferFee"/>, <see cref="AverageReviewRating"/>) belong to the store and an import
/// never overwrites them.
/// </summary>
public class FflDealer : AuditableEntity
{
    /// <summary>Directory key: region + district + sequence, e.g. "15601234" (legacy <c>Helper::getDealerKey</c>).</summary>
    public string Key { get; set; } = string.Empty;

    // ATF licence number parts: R-DD-CCC-TT-XX-SSSSS.
    public string LicRegn { get; set; } = string.Empty;
    public string LicDist { get; set; } = string.Empty;
    public string LicCnty { get; set; } = string.Empty;
    public string LicType { get; set; } = string.Empty;
    /// <summary>Expiry code: last digit of the year + month letter (A = January … H = August, J = September … M = December; no I).</summary>
    public string LicXprdte { get; set; } = string.Empty;
    public string LicSeqn { get; set; } = string.Empty;

    public string? LicenseName { get; set; }
    public string? BusinessName { get; set; }
    public string? PremiseStreet { get; set; }
    public string? PremiseCity { get; set; }
    public string? PremiseState { get; set; }
    public string? PremiseZipCode { get; set; }
    public string? MailStreet { get; set; }
    public string? MailCity { get; set; }
    public string? MailState { get; set; }
    public string? MailZipCode { get; set; }
    public string? VoicePhone { get; set; }

    /// <summary>Geocode of the premise (or its ZIP centroid). Dealers without one never appear in a radius search.</summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    /// <summary>False when the dealer was missing from the latest full import. Kept (not deleted) so orders and dealer data still resolve.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime? LastImportedOnUtc { get; set; }

    /// <summary>
    /// Added by hand on the admin page rather than from the ATF list, so not yet verified against it. A full import
    /// with "deactivate missing" leaves it active; an import row with the same key clears the flag (it is now an
    /// ATF record).
    /// </summary>
    public bool IsManual { get; set; }

    // Store-maintained dealer data (legacy dealer_data).
    /// <summary>Null = not recorded (treated as yes). False hides the dealer from every search (FFL-96).</summary>
    public bool? AllowsTransfers { get; set; }
    public decimal? LastTransferFee { get; set; }
    public decimal? AverageReviewRating { get; set; }
    public DateTime? DealerDataUpdatedOnUtc { get; set; }
}

/// <summary>
/// Cached geocode of a ZIP code (legacy <c>geocode_cache</c> with <c>zip_only = 1</c>): the centre the FFL
/// radius search measures from. Filled from the geocoding provider, or from the directory's own dealers.
/// </summary>
public class ZipGeocode : AuditableEntity
{
    /// <summary>Five-digit US ZIP.</summary>
    public string Zip { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    /// <summary>Where the point came from: "Geocodio", "Directory" (average of the dealers in that ZIP) or "Legacy" (PHP geocode_cache, db-scripts/25).</summary>
    public string Source { get; set; } = string.Empty;
}
