using System.Globalization;
using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Utilities;
using HKPartsEcommerce.Application.Features.Analytics;
using HKPartsEcommerce.Application.Features.Shipping;

namespace HKPartsEcommerce.Application.Features.Reports;

/// <summary>One ship-to state: recognised orders and revenue, split into guest-checkout and registered (A-41).</summary>
public class SalesByStateRowDto
{
    public string State { get; set; } = string.Empty;
    public string? CountryCode { get; set; }
    public int Orders { get; set; }
    public decimal Revenue { get; set; }
    public int GuestOrders { get; set; }
    public decimal GuestRevenue { get; set; }
    public int RegisteredOrders { get; set; }
    public decimal RegisteredRevenue { get; set; }
}

/// <summary>
/// Sales by State (guest checkout A-40/A-41): recognised (captured, non-cancelled) orders placed in the period,
/// grouped by the ship-to state, with guest orders included and split out. US states are merged whatever their
/// spelling ("TX" / "Texas"); a missing state shows as "Unknown". <see cref="CustomerType"/> narrows the rows to
/// guest or registered orders.
/// </summary>
public record GetSalesByStateReportQuery(
    string? Period = null,
    DateTime? From = null,
    DateTime? To = null,
    string? CustomerType = null) : IRequest<IReadOnlyList<SalesByStateRowDto>>;

public class GetSalesByStateReportQueryHandler : IRequestHandler<GetSalesByStateReportQuery, IReadOnlyList<SalesByStateRowDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public GetSalesByStateReportQueryHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SalesByStateRowDto>> Handle(GetSalesByStateReportQuery request, CancellationToken ct)
    {
        var period = AnalyticsPeriod.Resolve(request.Period, request.From, request.To, _clock.UtcNow);
        var orders = _db.Orders.AsNoTracking()
            .Where(o => o.PlacedOnUtc >= period.StartUtc && o.PlacedOnUtc < period.EndUtc)
            .WithRecognisedRevenue()
            .OfCustomerType(request.CustomerType);

        // Grouped in SQL on the raw values; the spelling variants are merged below.
        var raw = await orders
            .GroupBy(o => new
            {
                State = o.ShippingAddress != null ? o.ShippingAddress.StateProvince : null,
                Country = o.ShippingAddress != null ? o.ShippingAddress.CountryCode : null,
                Guest = o.GuestId != null || (o.CustomerId == null && o.EbayOrderId == null),
            })
            .Select(g => new { g.Key.State, g.Key.Country, g.Key.Guest, Count = g.Count(), Revenue = g.Sum(o => o.TotalAmount) })
            .ToListAsync(ct);

        return raw
            .GroupBy(r =>
            {
                var country = string.IsNullOrWhiteSpace(r.Country) ? null : r.Country.Trim().ToUpperInvariant();
                var state = string.IsNullOrWhiteSpace(r.State)
                    ? "Unknown"
                    : country is null or "US"
                        ? ShippingRulesEngine.NormalizeUsState(r.State) ?? r.State.Trim().ToUpperInvariant()
                        : r.State.Trim();
                return (State: state, Country: country);
            })
            .Select(g => new SalesByStateRowDto
            {
                State = g.Key.State,
                CountryCode = g.Key.Country,
                Orders = g.Sum(x => x.Count),
                Revenue = g.Sum(x => x.Revenue),
                GuestOrders = g.Where(x => x.Guest).Sum(x => x.Count),
                GuestRevenue = g.Where(x => x.Guest).Sum(x => x.Revenue),
                RegisteredOrders = g.Where(x => !x.Guest).Sum(x => x.Count),
                RegisteredRevenue = g.Where(x => !x.Guest).Sum(x => x.Revenue),
            })
            .OrderByDescending(r => r.Revenue)
            .ThenBy(r => r.State)
            .ToList();
    }

    public static byte[] ToCsv(IEnumerable<SalesByStateRowDto> rows)
    {
        static string N(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        var csv = Csv.Write(
            new[] { "State", "Country", "Orders", "Revenue", "Guest orders", "Guest revenue", "Registered orders", "Registered revenue" },
            rows.Select(r => new string?[]
            {
                r.State, r.CountryCode,
                r.Orders.ToString(CultureInfo.InvariantCulture), N(r.Revenue),
                r.GuestOrders.ToString(CultureInfo.InvariantCulture), N(r.GuestRevenue),
                r.RegisteredOrders.ToString(CultureInfo.InvariantCulture), N(r.RegisteredRevenue),
            }));
        return Encoding.UTF8.GetBytes(csv);
    }
}
