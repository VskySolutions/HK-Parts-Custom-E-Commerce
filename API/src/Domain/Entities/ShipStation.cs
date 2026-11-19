using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Singleton ShipStation "Custom Store" settings (docs/shipstation-verification-requirements.md §5.1, replacing
/// the legacy <c>shipstation_*</c> settings rows). ShipStation polls <c>/shipstation</c> with these credentials
/// to pull orders (<c>action=export</c>) and report shipments (<c>action=shipnotify</c>).
/// </summary>
public class ShipStationSettings : AuditableEntity
{
    public const string DefaultTimeZone = "America/New_York";
    public const string DefaultExportStatuses = "Pending,Processing,Shipped,Cancelled";

    /// <summary>The endpoint refuses every call while off (SS-03).</summary>
    public bool Enabled { get; set; }

    public string? Username { get; set; }

    /// <summary>Encrypted at rest and never sent back to the browser (SS-50).</summary>
    [Encrypted] public string? Password { get; set; }

    /// <summary>Export orders that came from eBay (SS-11).</summary>
    public bool IncludeEbay { get; set; }

    /// <summary>A mixed FFL order exports as two ShipStation orders: the number and the number + "-FFL" (SS-31).</summary>
    public bool SplitFfl { get; set; } = true;

    /// <summary>Email the buyer when a ship notice ships items (SS-46).</summary>
    public bool NotifyCustomer { get; set; } = true;

    /// <summary>
    /// Comma-separated site statuses exported: Pending, Processing, Shipped, Cancelled, OnHold (§5.1).
    /// OnHold = orders whose payment is held for review.
    /// </summary>
    public string ExportStatuses { get; set; } = DefaultExportStatuses;

    /// <summary>The zone ShipStation's start/end dates and the exported dates are in (SS-14). IANA or Windows id.</summary>
    public string TimeZone { get; set; } = DefaultTimeZone;

    /// <summary>
    /// Also accept the legacy <c>SS-UserName</c> / <c>SS-Password</c> query parameters (SS-04), for one
    /// release while the ShipStation store is moved to Basic auth.
    /// </summary>
    public bool AllowQueryCredentials { get; set; } = true;

    /// <summary>Full request/response payloads are logged until this time (SS-51); null = off. Set to now + 24 h.</summary>
    public DateTime? DebugUntilUtc { get; set; }
}

/// <summary>
/// One call ShipStation made to <c>/shipstation</c> (replaces <c>storage/logs/ShipStation.log</c>, SS-51):
/// action, window, counts, outcome and duration. No credentials, addresses or names are stored.
/// </summary>
public class ShipStationLog : BaseEntity
{
    /// <summary>"export", "shipnotify", or "auth" for a refused login.</summary>
    public string Action { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Page { get; set; }
    public int? OrderCount { get; set; }
    public string? OrderNumber { get; set; }
    public int HttpStatus { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
    public string? ClientIp { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
