namespace HKPartsEcommerce.Application.Features.Ffl;

/// <summary>One dealer as the storefront and admin see it (FFL shipping requirements §6.1 "Search response item").</summary>
public class FflDealerDto
{
    public string Key { get; set; } = string.Empty;
    /// <summary>Business name, else licence name (FFL-40).</summary>
    public string Name { get; set; } = string.Empty;
    public string? LicenseName { get; set; }
    public string? BusinessName { get; set; }
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    /// <summary>Raw phone digits from the directory.</summary>
    public string? Phone { get; set; }
    /// <summary>"(xxx) xxx-xxxx" for a 10-digit number, otherwise the raw value (FFL-42/46).</summary>
    public string? PhoneDisplay { get; set; }
    public double? DistanceMiles { get; set; }
    public string LicenseType { get; set; } = string.Empty;
    /// <summary>Full licence number R-DD-CCC-TT-XX-SSSSS.</summary>
    public string LicenseNumber { get; set; } = string.Empty;
    /// <summary>Expiry month as "yyyy-MM" (end of that month), null when the code cannot be read.</summary>
    public string? LicenseExpires { get; set; }
    public bool LicenseExpired { get; set; }
    public bool AllowsTransfers { get; set; }
    /// <summary>True only when the store has explicitly recorded the dealer's transfer policy.</summary>
    public bool AllowsTransfersRecorded { get; set; }
    public decimal? LastTransferFee { get; set; }
    public decimal? AverageReviewRating { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Can receive a checkout shipment: active, not expired, transfers not refused (FFL-26/59).</summary>
    public bool IsEligible { get; set; }
    public string? IneligibleReason { get; set; }
    public string MapUrl { get; set; } = string.Empty;
    public bool HasGeocode { get; set; }
    /// <summary>Added by hand on the admin page, not (yet) confirmed by an ATF import.</summary>
    public bool IsManual { get; set; }
}

/// <summary>Result of a radius search.</summary>
public class FflSearchResultDto
{
    public string Zip { get; set; } = string.Empty;
    public int Radius { get; set; }
    public List<FflDealerDto> Dealers { get; set; } = new();
    /// <summary>Dealers found before the result cap (FFL-39).</summary>
    public int TotalFound { get; set; }
    public bool Truncated { get; set; }
    /// <summary>User-facing note, e.g. the no-results message (FFL-37). Null when dealers were found.</summary>
    public string? Message { get; set; }
}

/// <summary>Where a search was run from: decides which radius list is allowed (FFL-36).</summary>
public enum FflSearchContext
{
    Checkout = 0,
    Public = 1,
    Admin = 2
}

/// <summary>User-facing messages (§7) and machine-readable codes for the 409 body.</summary>
public static class FflMessages
{
    public const string DealerRequired = "Please select an FFL dealer.";
    public const string UsOnly = "Items that require shipping to an authorized FFL dealer can only be billed and shipped to the United States";
    public const string InvalidZip = "Please enter a valid 5-digit ZIP code.";
    public const string SearchFailed = "We couldn't search dealers right now. Please try again.";
    public const string DealerLoadFailed = "We couldn't load that dealer. Please search again or choose another dealer.";
    public const string DealerNotEligible = "This dealer can't accept transfers. Please choose another dealer.";
    public const string AdminDealerChanged = "Shipping address updated. Please verify FFL details.";
    public const string AdminDealerDataSaved = "Dealer Data updated successfully";

    public static string NoResults(int radius, string zip) =>
        $"No dealers found within {radius} miles of {zip}. Try a larger radius.";

    public const string DealerRequiredCode = "FFL_DEALER_REQUIRED";
    public const string DealerNotEligibleCode = "FFL_DEALER_NOT_ELIGIBLE";
    public const string UsOnlyCode = "FFL_US_ONLY";
}
