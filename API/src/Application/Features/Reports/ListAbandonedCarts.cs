using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Application.Features.Analytics;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>One abandoned cart (guest checkout A-40/A-43): who it belongs to, what is in it, and when it went quiet.</summary>
public class AbandonedCartDto
{
    public Guid CartId { get; set; }
    public bool IsGuest { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Name { get; set; }

    /// <summary>The account email, or the email a guest typed at checkout (A-43); null when a guest never typed one.</summary>
    public string? Email { get; set; }

    /// <summary>N-06: a guest only gets a recovery email when they ticked "Email me news and offers".</summary>
    public bool MarketingOptIn { get; set; }
    public int ItemCount { get; set; }
    public int Units { get; set; }
    public decimal Subtotal { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime CreatedOnUtc { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public bool ReachedCheckout { get; set; }
    public DateTime? RecoveryEmailSentUtc { get; set; }
}

/// <summary>
/// Abandoned Carts (A-40/A-43): open carts with items and no activity for <see cref="IdleHours"/>, newest activity
/// first. Includes guest carts (staff can follow up when the guest typed an email at checkout) and customer carts.
/// Search matches the email or the customer's name.
/// </summary>
public record ListAbandonedCartsQuery(
    int Page = 1,
    int PageSize = 25,
    int IdleHours = 1,
    string? CustomerType = null,
    string? Search = null,
    bool OnlyWithEmail = false) : IRequest<PaginatedList<AbandonedCartDto>>;

public class ListAbandonedCartsQueryHandler : IRequestHandler<ListAbandonedCartsQuery, PaginatedList<AbandonedCartDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ListAbandonedCartsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PaginatedList<AbandonedCartDto>> Handle(ListAbandonedCartsQuery request, CancellationToken ct)
    {
        var page = await PaginatedList<AbandonedCartDto>.CreateAsync(
            Build(_db, _clock.UtcNow, request), request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
        return page;
    }

    internal static IQueryable<AbandonedCartDto> Build(IApplicationDbContext db, DateTime now, ListAbandonedCartsQuery request)
    {
        var cutoff = now.AddHours(-Math.Clamp(request.IdleHours, 0, 24 * 365));
        var rows = db.Carts.AsNoTracking()
            .Where(c => !c.IsCheckedOut && c.Items.Any())
            .Select(c => new
            {
                Cart = c,
                LastItem = c.Items.Max(i => (DateTime?)i.UpdatedOnUtc),
                CustomerEmail = c.Customer != null && c.Customer.User != null ? c.Customer.User.Email : null,
                CustomerName = c.Customer != null ? c.Customer.FirstName + " " + c.Customer.LastName : null,
            })
            .Select(x => new AbandonedCartDto
            {
                CartId = x.Cart.Id,
                IsGuest = x.Cart.CustomerId == null,
                CustomerId = x.Cart.CustomerId,
                Name = x.CustomerName,
                Email = x.CustomerEmail ?? x.Cart.ContactEmail,
                MarketingOptIn = x.Cart.CustomerId == null && x.Cart.MarketingOptIn,
                ItemCount = x.Cart.Items.Count,
                Units = x.Cart.Items.Sum(i => i.Quantity),
                Subtotal = x.Cart.Items.Sum(i => i.UnitPrice * i.Quantity),
                CurrencyCode = x.Cart.CurrencyCode,
                CreatedOnUtc = x.Cart.CreatedOnUtc,
                LastActivityUtc = x.Cart.LastCheckoutActivityUtc != null && x.Cart.LastCheckoutActivityUtc > (x.LastItem ?? x.Cart.UpdatedOnUtc)
                    ? x.Cart.LastCheckoutActivityUtc.Value
                    : (x.LastItem != null && x.LastItem > x.Cart.UpdatedOnUtc ? x.LastItem.Value : x.Cart.UpdatedOnUtc),
                ReachedCheckout = x.Cart.LastCheckoutActivityUtc != null,
                RecoveryEmailSentUtc = x.Cart.AbandonmentNotifiedOnUtc,
            })
            .Where(r => r.LastActivityUtc < cutoff);

        if (string.Equals(request.CustomerType, CustomerTypes.Guest, StringComparison.OrdinalIgnoreCase))
            rows = rows.Where(r => r.IsGuest);
        else if (string.Equals(request.CustomerType, CustomerTypes.Registered, StringComparison.OrdinalIgnoreCase))
            rows = rows.Where(r => !r.IsGuest);
        if (request.OnlyWithEmail)
            rows = rows.Where(r => r.Email != null);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(r => (r.Email != null && r.Email.Contains(term)) || (r.Name != null && r.Name.Contains(term)));
        }
        return rows.OrderByDescending(r => r.LastActivityUtc);
    }
}

/// <summary>A-43: the filtered abandoned carts as CSV (up to 50,000 rows).</summary>
public record ExportAbandonedCartsQuery(ListAbandonedCartsQuery Filter) : IRequest<byte[]>;

public class ExportAbandonedCartsQueryHandler : IRequestHandler<ExportAbandonedCartsQuery, byte[]>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public ExportAbandonedCartsQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<byte[]> Handle(ExportAbandonedCartsQuery request, CancellationToken ct)
    {
        var rows = await ListAbandonedCartsQueryHandler.Build(_db, _clock.UtcNow, request.Filter).Take(50000).ToListAsync(ct);
        static string D(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
        var csv = Csv.Write(
            new[] { "Type", "Name", "Email", "Marketing opt-in", "Items", "Units", "Subtotal", "Currency", "Reached checkout", "Last activity (UTC)", "Recovery email sent (UTC)" },
            rows.Select(r => new string?[]
            {
                r.IsGuest ? "Guest" : "Registered", r.Name?.Trim(), r.Email, r.IsGuest ? (r.MarketingOptIn ? "Yes" : "No") : "",
                r.ItemCount.ToString(CultureInfo.InvariantCulture), r.Units.ToString(CultureInfo.InvariantCulture),
                r.Subtotal.ToString("0.00", CultureInfo.InvariantCulture), r.CurrencyCode,
                r.ReachedCheckout ? "Yes" : "No", D(r.LastActivityUtc), D(r.RecoveryEmailSentUtc),
            }));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }
}
