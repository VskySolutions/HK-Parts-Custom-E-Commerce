using System.Globalization;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShipStation;

/// <summary>
/// Admin view of the ShipStation screen (§5.1). The password is write-only (SS-50): only whether one is set
/// is ever returned.
/// </summary>
public class ShipStationSettingsDto
{
    public bool Enabled { get; set; }
    public string? Username { get; set; }
    public bool PasswordSet { get; set; }
    public bool IncludeEbay { get; set; }
    public bool SplitFfl { get; set; }
    public bool NotifyCustomer { get; set; }
    public List<string> ExportStatuses { get; set; } = new();
    public string TimeZone { get; set; } = ShipStationSettings.DefaultTimeZone;
    public bool AllowQueryCredentials { get; set; }
    public bool DebugLogging { get; set; }
    public DateTime? DebugUntilUtc { get; set; }

    /// <summary>The URL to paste into ShipStation's Custom Store setup (SS-07).</summary>
    public string? FeedUrl { get; set; }

    public ShipStationLogDto? LastExport { get; set; }
    public ShipStationLogDto? LastShipNotify { get; set; }
    public DateTime? UpdatedOnUtc { get; set; }

    public static ShipStationSettingsDto From(ShipStationSettings s, DateTime nowUtc) => new()
    {
        Enabled = s.Enabled,
        Username = s.Username,
        PasswordSet = !string.IsNullOrEmpty(s.Password),
        IncludeEbay = s.IncludeEbay,
        SplitFfl = s.SplitFfl,
        NotifyCustomer = s.NotifyCustomer,
        ExportStatuses = ShipStationSupport.ParseStatuses(s.ExportStatuses).ToList(),
        TimeZone = s.TimeZone,
        AllowQueryCredentials = s.AllowQueryCredentials,
        DebugLogging = s.DebugUntilUtc > nowUtc,
        DebugUntilUtc = s.DebugUntilUtc > nowUtc ? s.DebugUntilUtc : null,
        UpdatedOnUtc = s.UpdatedOnUtc == default ? null : s.UpdatedOnUtc,
    };
}

/// <summary>One logged ShipStation call (SS-51): no credentials, names or addresses.</summary>
public class ShipStationLogDto
{
    public Guid Id { get; set; }
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

    public static ShipStationLogDto From(ShipStationLog l) => new()
    {
        Id = l.Id, Action = l.Action, StartDate = l.StartDate, EndDate = l.EndDate, Page = l.Page,
        OrderCount = l.OrderCount, OrderNumber = l.OrderNumber, HttpStatus = l.HttpStatus,
        DurationMs = l.DurationMs, Error = l.Error, ClientIp = l.ClientIp, CreatedOnUtc = l.CreatedOnUtc,
    };
}

/// <summary>The status names the admin picks from (§5.1) and how site orders map onto them (SS-25).</summary>
public static class ShipStationStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Shipped = "Shipped";
    public const string Cancelled = "Cancelled";
    public const string OnHold = "OnHold";

    public static readonly string[] All = { Pending, Processing, Shipped, Cancelled, OnHold };

    /// <summary>Site order statuses behind each exported status name.</summary>
    public static IReadOnlyList<OrderStatus> OrderStatusesFor(string name) => name switch
    {
        Pending => new[] { OrderStatus.Pending, OrderStatus.PendingRouting, OrderStatus.Routed, OrderStatus.Accepted },
        Processing => new[] { OrderStatus.Processing, OrderStatus.Preparing },
        Shipped => new[] { OrderStatus.Shipped, OrderStatus.Delivered },
        Cancelled => new[] { OrderStatus.Cancelled },
        _ => Array.Empty<OrderStatus>(),
    };

    /// <summary>
    /// The ShipStation status sent for an order (SS-25), documented on the admin screen so the ShipStation
    /// store's status mapping matches: pending/processing → "awaiting_shipment", shipped →
    /// "shipped", cancelled → "cancelled", payment held for review → "on_hold".
    /// </summary>
    public static string ShipStationStatus(OrderStatus status, PaymentStatus payment) =>
        status switch
        {
            OrderStatus.Cancelled => "cancelled",
            OrderStatus.Shipped or OrderStatus.Delivered => "shipped",
            _ when payment == PaymentStatus.HeldForReview => "on_hold",
            _ => "awaiting_shipment",
        };

    /// <summary>Payment states that count as "paid = 1" (SS-10).</summary>
    public static readonly PaymentStatus[] Paid =
        { PaymentStatus.Captured, PaymentStatus.Authorized, PaymentStatus.PartiallyRefunded };

    /// <summary>A cancelled order is exported when it was paid at some point, so ShipStation can cancel it (SS-25).</summary>
    public static readonly PaymentStatus[] PaidThenCancelled =
        { PaymentStatus.Captured, PaymentStatus.Authorized, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded, PaymentStatus.Voided };
}

public static class ShipStationSupport
{
    /// <summary>Most orders per export page (SS-24).</summary>
    public const int PageSize = 100;

    /// <summary>The date format ShipStation sends and expects (SS-23).</summary>
    public const string DateFormat = "MM/dd/yyyy HH:mm";

    /// <summary>Order-number suffix of the FFL half of a split order (SS-31).</summary>
    public const string FflSuffix = "-FFL";

    /// <summary>The settings row, or the defaults (off) when never saved. Read-only.</summary>
    public static async Task<ShipStationSettings> LoadAsync(IApplicationDbContext db, CancellationToken ct) =>
        await db.ShipStationSettings.AsNoTracking().OrderBy(s => s.CreatedOnUtc).FirstOrDefaultAsync(ct)
        ?? new ShipStationSettings();

    public static IReadOnlyList<string> ParseStatuses(string? csv)
    {
        var picked = ShippingRulesEngine.SplitCodes(csv);
        return ShipStationStatuses.All.Where(s => picked.Contains(s.ToUpperInvariant())).ToList();
    }

    public static TimeZoneInfo ResolveTimeZone(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var tz)
            ? tz
            : TimeZoneInfo.TryFindSystemTimeZoneById(ShipStationSettings.DefaultTimeZone, out var ny) ? ny : TimeZoneInfo.Utc;

    private static readonly string[] AcceptedDateFormats =
    {
        "MM/dd/yyyy HH:mm", "M/d/yyyy H:mm", "MM/dd/yyyy HH:mm:ss", "M/d/yyyy H:mm:ss",
        "MM/dd/yyyy hh:mm tt", "M/d/yyyy h:mm tt", "MM/dd/yyyy", "M/d/yyyy",
        "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd",
    };

    /// <summary>Parses a ShipStation date (store local time, SS-14) and returns it in UTC; null when unreadable.</summary>
    public static DateTime? ParseLocalToUtc(string? value, TimeZoneInfo zone)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!DateTime.TryParseExact(value.Trim(), AcceptedDateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var local))
            return null;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        try
        {
            return TimeZoneInfo.ConvertTimeToUtc(local, zone);
        }
        catch (ArgumentException)
        {
            // A wall-clock time skipped by a DST change: move an hour on.
            return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(1), zone);
        }
    }

    /// <summary>A UTC instant in store local time, as ShipStation reads it (SS-23).</summary>
    public static string FormatLocal(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            .ToString(DateFormat, CultureInfo.InvariantCulture);

    /// <summary>Money with a dot and no thousands separator: 1250.00 (SS-22).</summary>
    public static string Money(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Pounds as a decimal (SS-29).</summary>
    public static string Weight(decimal pounds) =>
        decimal.Round(pounds, 4, MidpointRounding.AwayFromZero).ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>Splits "100045-FFL" into ("100045", true) (SS-32).</summary>
    public static (string OrderNumber, bool IsFfl) SplitOrderNumber(string orderNumber)
    {
        var trimmed = orderNumber.Trim();
        return trimmed.EndsWith(FflSuffix, StringComparison.OrdinalIgnoreCase)
            ? (trimmed[..^FflSuffix.Length], true)
            : (trimmed, false);
    }
}
