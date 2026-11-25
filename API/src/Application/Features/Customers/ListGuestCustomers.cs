using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// One guest buyer (guest checkout §4.2): a <see cref="Guest"/> row, keyed by the normalized email (A-12), with
/// totals over every order they placed as a guest, including orders since moved to an account they registered.
/// Name and phone are from the latest order; location from its ship-to.
/// </summary>
public class GuestCustomerDto
{
    /// <summary>Opaque id the admin APIs use (A-15): the email never goes in a URL.</summary>
    public Guid GuestId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? CountryCode { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime FirstOrderUtc { get; set; }
    public DateTime LastOrderUtc { get; set; }
    public string? LastOrderNumber { get; set; }
    public bool MarketingOptIn { get; set; }

    /// <summary>A-13: an active blacklist entry matches this guest (email, or first + last name).</summary>
    public bool IsBlocked { get; set; }

    /// <summary>The matching blocked entry, so the screen can unblock it (A-13).</summary>
    public Guid? BlacklistEntryId { get; set; }

    /// <summary>A-18: the account id when this guest has since registered (linked, or an account with the same email).</summary>
    public Guid? RegisteredCustomerId { get; set; }

    /// <summary>The guest has registered an account (they stay on this list, marked as registered).</summary>
    public bool IsRegistered { get; set; }

    /// <summary>When the guest orders were moved onto the account; null while they are not linked.</summary>
    public DateTime? LinkedOnUtc { get; set; }

    /// <summary>Guest orders not yet on the account: "Link orders" is offered while this is above 0.</summary>
    public int UnlinkedOrderCount { get; set; }

    /// <summary>N-05: the guest's personal data was anonymised on request (when).</summary>
    public DateTime? AnonymizedOnUtc { get; set; }
}

/// <summary>A guest's order for the drill-down (A-14).</summary>
public class GuestOrderDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime PlacedOnUtc { get; set; }
    public string? ShipTo { get; set; }
    public string? FflNumber { get; set; }

    /// <summary>The order was placed as a guest and has since been moved onto the registered account.</summary>
    public bool LinkedToAccount { get; set; }
}

/// <summary>Status filter of the Guest Customers screen (A-13).</summary>
public enum GuestStatusFilter
{
    All = 0,
    Active = 1,
    Blocked = 2,
}

/// <summary>Account filter of the Guest Customers screen: guests who registered stay listed.</summary>
public enum GuestAccountFilter
{
    All = 0,
    /// <summary>No account for this email yet.</summary>
    GuestOnly = 1,
    /// <summary>Registered an account after ordering as a guest.</summary>
    Registered = 2,
}

internal static class GuestOrders
{
    /// <summary>
    /// Storefront orders placed as a guest (they carry a guest row), whether or not they have since been moved onto
    /// the account the guest registered. Not eBay imports, inquiries or abandoned off-site payment attempts (same
    /// exclusions as the admin Orders list).
    /// </summary>
    public static IQueryable<Order> Query(IApplicationDbContext db) =>
        db.Orders.AsNoTracking()
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .Where(o => o.EbayOrderId == null && o.GuestId != null);
}

internal sealed class GuestRow
{
    public Guest Guest { get; set; } = null!;
    public int OrderCount { get; set; }
    public int UnlinkedOrderCount { get; set; }
    public decimal TotalSpent { get; set; }
    public DateTime FirstOrderUtc { get; set; }
    public DateTime LastOrderUtc { get; set; }
    public Guid? BlacklistEntryId { get; set; }
}

internal static class GuestListQuery
{
    /// <summary>The filtered, newest-first guest rows behind the list and the export (A-10, A-11, A-13).</summary>
    public static IQueryable<GuestRow> Build(
        IApplicationDbContext db, string? search, GuestStatusFilter status, GuestAccountFilter account = GuestAccountFilter.All)
    {
        var orders = GuestOrders.Query(db);
        var blocked = db.CustomerBlacklistEntries.Where(b => b.Status == CustomerBlacklistStatus.Blocked);
        var users = db.Users.AsNoTracking();

        // Sub-queries are written inline (not projected as members), so EF translates them to SQL.
        var guests = db.Guests.AsNoTracking().Where(g => orders.Any(o => o.GuestId == g.Id));

        // Registered = the guest's orders were linked to an account, or an account exists with the guest's email.
        guests = account switch
        {
            GuestAccountFilter.Registered => guests.Where(g => g.LinkedCustomerId != null || users.Any(u => u.Email == g.EmailNormalized)),
            GuestAccountFilter.GuestOnly => guests.Where(g => g.LinkedCustomerId == null && !users.Any(u => u.Email == g.EmailNormalized)),
            _ => guests,
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            guests = guests.Where(g => g.EmailNormalized.Contains(term)
                                       || (g.FirstName != null && g.FirstName.Contains(term))
                                       || (g.LastName != null && g.LastName.Contains(term))
                                       || (g.PhoneNumber != null && g.PhoneNumber.Contains(term))
                                       || orders.Any(o => o.GuestId == g.Id && o.OrderNumber.Contains(term)));
        }

        var rows = guests.Select(g => new
        {
            Guest = g,
            EntryId = blocked
                .Where(b => (b.Email != null && b.Email == g.EmailNormalized)
                            || (b.FirstName != null && b.LastName != null && g.FirstName != null && g.LastName != null
                                && b.FirstName == g.FirstName && b.LastName == g.LastName))
                .Select(b => (Guid?)b.Id)
                .FirstOrDefault(),
        });

        rows = status switch
        {
            GuestStatusFilter.Blocked => rows.Where(x => x.EntryId != null),
            GuestStatusFilter.Active => rows.Where(x => x.EntryId == null),
            _ => rows,
        };

        return rows
            .Select(x => new GuestRow
            {
                Guest = x.Guest,
                OrderCount = orders.Count(o => o.GuestId == x.Guest.Id),
                UnlinkedOrderCount = orders.Count(o => o.GuestId == x.Guest.Id && o.CustomerId == null),
                TotalSpent = orders.Where(o => o.GuestId == x.Guest.Id).Sum(o => o.TotalAmount),
                FirstOrderUtc = orders.Where(o => o.GuestId == x.Guest.Id).Min(o => o.PlacedOnUtc),
                LastOrderUtc = orders.Where(o => o.GuestId == x.Guest.Id).Max(o => o.PlacedOnUtc),
                BlacklistEntryId = x.EntryId,
            })
            .OrderByDescending(x => x.LastOrderUtc);
    }

    /// <summary>Location, currency and latest order number for the page, plus which emails now have an account.</summary>
    public static async Task<List<GuestCustomerDto>> ToDtosAsync(IApplicationDbContext db, IReadOnlyList<GuestRow> rows, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Guest.Id).ToList();
        var latest = (await GuestOrders.Query(db)
                .Where(o => ids.Contains(o.GuestId!.Value))
                .OrderByDescending(o => o.PlacedOnUtc)
                .Select(o => new
                {
                    GuestId = o.GuestId!.Value,
                    o.OrderNumber,
                    o.CurrencyCode,
                    City = o.ShippingAddress != null ? o.ShippingAddress.City : null,
                    State = o.ShippingAddress != null ? o.ShippingAddress.StateProvince : null,
                    Country = o.ShippingAddress != null ? o.ShippingAddress.CountryCode : null,
                })
                .ToListAsync(ct))
            .GroupBy(x => x.GuestId)
            .ToDictionary(g => g.Key, g => g.First());

        var emails = rows.Select(r => r.Guest.EmailNormalized).ToList();
        var accounts = (await db.Customers.AsNoTracking()
                .Where(c => c.User != null && emails.Contains(c.User.Email))
                .Select(c => new { c.Id, Email = c.User!.Email })
                .ToListAsync(ct))
            .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        return rows.Select(r =>
        {
            latest.TryGetValue(r.Guest.Id, out var last);
            var accountId = r.Guest.LinkedCustomerId ?? (accounts.TryGetValue(r.Guest.EmailNormalized, out var id) ? id : (Guid?)null);
            return new GuestCustomerDto
            {
                GuestId = r.Guest.Id,
                Email = r.Guest.EmailNormalized,
                FirstName = r.Guest.FirstName,
                LastName = r.Guest.LastName,
                PhoneNumber = r.Guest.PhoneNumber,
                MarketingOptIn = r.Guest.MarketingOptIn,
                City = last?.City,
                StateProvince = last?.State,
                CountryCode = last?.Country,
                LastOrderNumber = last?.OrderNumber,
                CurrencyCode = last?.CurrencyCode ?? "USD",
                OrderCount = r.OrderCount,
                TotalSpent = r.TotalSpent,
                FirstOrderUtc = r.FirstOrderUtc,
                LastOrderUtc = r.LastOrderUtc,
                IsBlocked = r.BlacklistEntryId is not null,
                BlacklistEntryId = r.BlacklistEntryId,
                RegisteredCustomerId = accountId,
                IsRegistered = accountId is not null,
                LinkedOnUtc = r.Guest.LinkedOnUtc,
                UnlinkedOrderCount = r.UnlinkedOrderCount,
                AnonymizedOnUtc = r.Guest.AnonymizedOnUtc,
            };
        }).ToList();
    }
}

public record ListGuestCustomersQuery(int Page = 1, int PageSize = 25, string? Search = null, GuestStatusFilter Status = GuestStatusFilter.All,
    GuestAccountFilter Account = GuestAccountFilter.All)
    : IRequest<PaginatedList<GuestCustomerDto>>;

public class ListGuestCustomersQueryHandler : IRequestHandler<ListGuestCustomersQuery, PaginatedList<GuestCustomerDto>>
{
    private readonly IApplicationDbContext _db;

    public ListGuestCustomersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<GuestCustomerDto>> Handle(ListGuestCustomersQuery request, CancellationToken ct)
    {
        var page = await PaginatedList<GuestRow>.CreateAsync(
            GuestListQuery.Build(_db, request.Search, request.Status, request.Account), request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
        var items = await GuestListQuery.ToDtosAsync(_db, page.Items, ct);
        return new PaginatedList<GuestCustomerDto>(items, page.TotalCount, page.PageNumber, page.PageSize);
    }
}

/// <summary>A-17: the filtered guest list as CSV (UTF-8 with BOM so Excel opens it with the right characters) or Excel (.xlsx).</summary>
public record ExportGuestCustomersQuery(string? Search = null, GuestStatusFilter Status = GuestStatusFilter.All,
    GuestAccountFilter Account = GuestAccountFilter.All, GuestExportFormat Format = GuestExportFormat.Csv) : IRequest<byte[]>;

public class ExportGuestCustomersQueryHandler : IRequestHandler<ExportGuestCustomersQuery, byte[]>
{
    /// <summary>A hard cap so one click cannot pull an unbounded result into memory.</summary>
    public const int MaxRows = 50000;

    private readonly IApplicationDbContext _db;

    public ExportGuestCustomersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportGuestCustomersQuery request, CancellationToken ct)
    {
        var rows = await GuestListQuery.Build(_db, request.Search, request.Status, request.Account).Take(MaxRows).ToListAsync(ct);
        var dtos = await GuestListQuery.ToDtosAsync(_db, rows, ct);

        var header = new[]
        {
            "First name", "Last name", "Email", "Phone", "City", "State", "Country", "Orders", "Total spent", "Currency",
            "First order (UTC)", "Last order (UTC)", "Last order #", "Status", "Registered", "Orders linked (UTC)", "Marketing opt-in",
        };
        static string Utc(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        // Numbers stay numbers in Excel; CSV writes them as invariant text.
        var data = dtos.Select(d => (IReadOnlyList<object?>)new object?[]
        {
            d.FirstName, d.LastName, d.Email, d.PhoneNumber, d.City, d.StateProvince, d.CountryCode,
            d.OrderCount,
            d.TotalSpent,
            d.CurrencyCode,
            Utc(d.FirstOrderUtc),
            Utc(d.LastOrderUtc),
            d.LastOrderNumber,
            d.IsBlocked ? "Blocked" : "Active",
            d.IsRegistered ? "Yes" : "No",
            Utc(d.LinkedOnUtc),
            d.MarketingOptIn ? "Yes" : "No",
        }).ToList();

        if (request.Format == GuestExportFormat.Xlsx)
            return Xlsx.Write("Guest customers", header, data);

        var csv = Csv.Write(header, data.Select(r => r.Select(v => v switch
        {
            null => null,
            decimal m => m.ToString("0.00", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => v.ToString(),
        })));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }
}

/// <summary>A-17: the guest list export formats.</summary>
public enum GuestExportFormat
{
    Csv = 0,
    Xlsx = 1,
}

/// <summary>A-14 / A-15: a guest's orders by opaque guest id, newest first.</summary>
public record ListGuestCustomerOrdersQuery(Guid GuestId) : IRequest<List<GuestOrderDto>>;

public class ListGuestCustomerOrdersQueryHandler : IRequestHandler<ListGuestCustomerOrdersQuery, List<GuestOrderDto>>
{
    private readonly IApplicationDbContext _db;

    public ListGuestCustomerOrdersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<GuestOrderDto>> Handle(ListGuestCustomerOrdersQuery request, CancellationToken ct)
    {
        if (!await _db.Guests.AnyAsync(g => g.Id == request.GuestId, ct))
            throw new NotFoundException(nameof(Guest), request.GuestId);

        var rows = await GuestOrders.Query(_db)
            .Where(o => o.GuestId == request.GuestId)
            .OrderByDescending(o => o.PlacedOnUtc)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.PaymentStatus,
                o.TotalAmount,
                o.CurrencyCode,
                o.PlacedOnUtc,
                o.IsPickup,
                Linked = o.CustomerId != null,
                Ship = o.ShippingAddress,
                Paid = o.Payments
                    .Where(p => p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.PartiallyRefunded || p.Status == PaymentStatus.Refunded)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                Ffl = o.Shippings.Where(s => s.ContainsFflItems).Select(s => s.FflNumber ?? s.FflDealerKey).FirstOrDefault(),
            })
            .ToListAsync(ct);

        return rows.Select(o => new GuestOrderDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            Status = o.Status.ToString(),
            PaymentStatus = o.PaymentStatus.ToString(),
            TotalAmount = o.TotalAmount,
            PaidAmount = o.Paid,
            CurrencyCode = o.CurrencyCode,
            PlacedOnUtc = o.PlacedOnUtc,
            ShipTo = o.IsPickup ? "Pickup in store" : FormatShipTo(o.Ship),
            FflNumber = o.Ffl,
            LinkedToAccount = o.Linked,
        }).ToList();
    }

    private static string? FormatShipTo(Address? a) =>
        a is null ? null : string.Join(", ", new[] { a.AddressLine1, a.City, a.StateProvince, a.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>
/// A-18: move a guest's orders onto the account registered with the same email, after the admin confirms (N-07).
/// The account is found by the guest's email; a different account can't be chosen, so orders never land on a
/// stranger's account by mistake.
/// </summary>
public record LinkGuestOrdersCommand(Guid GuestId) : IRequest<LinkGuestOrdersResult>;

public record LinkGuestOrdersResult(Guid CustomerId, int LinkedOrders);

public class LinkGuestOrdersCommandHandler : IRequestHandler<LinkGuestOrdersCommand, LinkGuestOrdersResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService? _current;

    public LinkGuestOrdersCommandHandler(IApplicationDbContext db, IDateTimeProvider clock, ICurrentUserService? current = null)
    {
        _db = db;
        _clock = clock;
        _current = current;
    }

    public async Task<LinkGuestOrdersResult> Handle(LinkGuestOrdersCommand request, CancellationToken ct)
    {
        var guest = await _db.Guests.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.GuestId, ct)
                    ?? throw new NotFoundException(nameof(Guest), request.GuestId);
        var customerId = await _db.Customers
            .Where(c => c.User != null && c.User.Email == guest.EmailNormalized)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new ConflictException("No account is registered with this guest's email, so there is nothing to link the orders to.");

        var linked = await GuestRecords.LinkOrdersAsync(_db, guest.EmailNormalized, customerId, _clock.UtcNow, ct, _current?.UserId);
        await _db.SaveChangesAsync(ct);
        return new LinkGuestOrdersResult(customerId, linked);
    }
}
