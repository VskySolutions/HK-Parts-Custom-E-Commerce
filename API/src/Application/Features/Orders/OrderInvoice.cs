using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>An address block on the invoice, already split into printable lines.</summary>
public class InvoiceAddressDto
{
    public string? Name { get; set; }
    public string? Company { get; set; }
    public string? Line1 { get; set; }
    public string? Line2 { get; set; }
    /// <summary>"City, ST 12345".</summary>
    public string? CityLine { get; set; }
    public string? Phone { get; set; }
}

public class InvoiceLineDto
{
    public string? Sku { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>The buyer's typed custom-input values, printed under the name.</summary>
    public string? Options { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal SubTotal { get; set; }
}

/// <summary>
/// Everything the invoice shows (the PHP admin's <c>/orders/invoice/{id}</c> layout): order number and date,
/// billing and shipping blocks, the line table and the totals down to Amount Due. One source for the on-screen
/// invoice page and the PDF download, so the two can never disagree. Figures are read from the saved order,
/// never recalculated.
/// </summary>
public class InvoiceDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDateUtc { get; set; }
    /// <summary>The order date in the store's display time zone, "MM/dd/yyyy h:mm AM".</summary>
    public string OrderDateText { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";

    public InvoiceAddressDto Billing { get; set; } = new();
    public string? PaymentMethod { get; set; }

    public InvoiceAddressDto? Shipping { get; set; }
    /// <summary>"Standard Shipping - $9.50", or the pickup store for a pickup order.</summary>
    public string? ShippingMethod { get; set; }
    /// <summary>The FFL dealer the firearms ship to, for a mixed order (the buyer's items ship to <see cref="Shipping"/>).</summary>
    public InvoiceAddressDto? FflDealer { get; set; }
    /// <summary>The FFL shipment's method, for a mixed order.</summary>
    public string? FflShippingMethod { get; set; }
    public string? FflNumber { get; set; }
    /// <summary>FFL-only order: <see cref="Shipping"/> is the dealer's address.</summary>
    public bool ShipsToFflDealer { get; set; }
    public bool IsPickup { get; set; }

    public List<InvoiceLineDto> Lines { get; set; } = new();

    /// <summary>Sum of the line sub totals as printed.</summary>
    public decimal SubTotal { get; set; }
    /// <summary>Coupon / promotion discounts (order-level).</summary>
    public decimal Discount { get; set; }
    public decimal ShippingTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal PaymentFee { get; set; }
    public decimal Total { get; set; }
    /// <summary>Captured payments, gift certificates included (refunded ones too: they were paid).</summary>
    public decimal PaymentApplied { get; set; }
    public decimal Refunded { get; set; }
    public decimal AmountDue { get; set; }
}

public record GetOrderInvoiceQuery(Guid OrderId) : IRequest<InvoiceDto>;

public class GetOrderInvoiceQueryHandler : IRequestHandler<GetOrderInvoiceQuery, InvoiceDto>
{
    private readonly IApplicationDbContext _db;

    public GetOrderInvoiceQueryHandler(IApplicationDbContext db) => _db = db;

    public Task<InvoiceDto> Handle(GetOrderInvoiceQuery request, CancellationToken ct)
        => OrderInvoiceBuilder.BuildAsync(_db, request.OrderId, ct);
}

public static class OrderInvoiceBuilder
{
    private static readonly PaymentStatus[] PaidStatuses =
        { PaymentStatus.Captured, PaymentStatus.PartiallyRefunded, PaymentStatus.Refunded };

    public static async Task<InvoiceDto> BuildAsync(IApplicationDbContext db, Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.ShippingAddress)
            .Include(o => o.BillingAddress)
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Include(o => o.Shippings)
            .Include(o => o.AssignedStore)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        var timeZone = await db.TenantBrandings.AsNoTracking().Select(b => b.DisplayTimeZone).FirstOrDefaultAsync(ct);
        var tz = !string.IsNullOrWhiteSpace(timeZone) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var found)
            ? found
            : TimeZoneInfo.Utc;
        var placedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(order.PlacedOnUtc, DateTimeKind.Utc), tz);

        var shipTo = order.ShippingAddress;
        var shipping = shipTo is null ? null : ToAddress(shipTo, order.ContactName, order.ContactPhone);

        // The billing address confirmed at checkout; older orders (none stored) fall back to the customer's default
        // billing address from their address book, else the address the order shipped to.
        InvoiceAddressDto? billing = order.BillingAddress is { } ownBill
            ? ToAddress(ownBill, order.ContactName, order.ContactPhone)
            : null;
        if (billing is null && order.CustomerId is Guid customerId)
        {
            var billTo = await db.CustomerAddresses.AsNoTracking()
                .Where(a => a.CustomerId == customerId && a.AddressType == AddressType.Billing && a.Address != null)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.CreatedOnUtc)
                .Select(a => a.Address!)
                .FirstOrDefaultAsync(ct);
            if (billTo is not null)
                billing = ToAddress(billTo, order.ContactName, order.ContactPhone);
        }
        billing ??= shipping ?? new InvoiceAddressDto { Name = order.ContactName, Company = order.CompanyName, Phone = order.ContactPhone };

        var paid = order.Payments.Where(p => PaidStatuses.Contains(p.Status)).ToList();
        var methods = (paid.Count > 0 ? paid : order.Payments.Where(p => p.Status != PaymentStatus.Failed).ToList())
            .Select(p => PaymentMethodLabel(p.Method))
            .Distinct()
            .ToList();

        var lines = order.Lines.Select(l => new InvoiceLineDto
        {
            Sku = l.Sku,
            Name = l.ProductName,
            Options = CustomAttributes.Describe(l.CustomAttributesJson) is { Length: > 0 } opts ? opts : null,
            Quantity = l.Quantity,
            Price = l.UnitPrice,
            SubTotal = l.LineTotal,
        }).ToList();

        var dto = new InvoiceDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            OrderDateUtc = order.PlacedOnUtc,
            OrderDateText = placedLocal.ToString("MM/dd/yyyy h:mm tt", CultureInfo.InvariantCulture),
            CurrencyCode = order.CurrencyCode,
            Billing = billing,
            PaymentMethod = methods.Count > 0 ? string.Join(", ", methods) : null,
            IsPickup = order.IsPickup,
            Shipping = order.IsPickup ? null : shipping,
            Lines = lines,
            SubTotal = lines.Sum(l => l.SubTotal),
            Discount = order.DiscountTotal,
            ShippingTotal = order.ShippingTotal,
            Tax = order.TaxTotal,
            PaymentFee = order.PaymentFeeTotal,
            Total = order.TotalAmount,
            PaymentApplied = paid.Sum(p => p.Amount),
            Refunded = paid.Sum(p => p.RefundedAmount),
        };
        dto.AmountDue = Math.Max(0m, dto.Total - dto.PaymentApplied);

        string MethodText(string? name, decimal amount) =>
            string.IsNullOrWhiteSpace(name) ? Money(amount, order.CurrencyCode) : $"{name.Trim()} - {Money(amount, order.CurrencyCode)}";

        var ffl = order.Shippings.FirstOrDefault(s => s.ShipToType == ShipToType.FflDealer && !string.IsNullOrWhiteSpace(s.FflDealerName));
        var buyerShipment = order.Shippings.FirstOrDefault(s => s.ShipToType == ShipToType.Customer);

        if (order.IsPickup)
        {
            var store = order.AssignedStore;
            dto.ShippingMethod = store is null ? "Store pickup" : $"Store pickup: {store.Name}";
        }
        else if (ffl is not null)
        {
            var dealer = new InvoiceAddressDto
            {
                Name = ffl.FflDealerName,
                Line1 = ffl.FflDealerStreet,
                CityLine = CityLine(ffl.FflDealerCity, ffl.FflDealerState, ffl.FflDealerZip),
                Phone = ffl.FflDealerPhone,
            };
            dto.FflNumber = ffl.FflNumber;
            var fflMethod = MethodText(ffl.DisplayName ?? ffl.MethodName, ffl.ShippingTotal);
            if (buyerShipment is null)
            {
                // FFL-only: everything ships to the dealer.
                dto.ShipsToFflDealer = true;
                dto.Shipping = dealer;
                dto.ShippingMethod = fflMethod;
            }
            else
            {
                // Mixed: the buyer's items ship to the buyer, the firearms to the dealer, each with its own method.
                dto.ShippingMethod = MethodText(buyerShipment.DisplayName ?? buyerShipment.MethodName, buyerShipment.ShippingTotal);
                dto.FflDealer = dealer;
                dto.FflShippingMethod = fflMethod;
            }
        }
        else if (!string.IsNullOrWhiteSpace(order.ShippingMethodName))
        {
            dto.ShippingMethod = MethodText(order.ShippingMethodName, order.ShippingTotal);
        }

        return dto;
    }

    /// <summary>"$249.99" for USD (the PHP invoice), else "EUR 249.99".</summary>
    public static string Money(decimal amount, string currencyCode)
    {
        var n = Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture);
        var sign = amount < 0 ? "-" : string.Empty;
        return string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase) ? $"{sign}${n}" : $"{sign}{currencyCode} {n}";
    }

    public static string PaymentMethodLabel(PaymentMethodType method) => method switch
    {
        PaymentMethodType.Stripe or PaymentMethodType.Square or PaymentMethodType.AuthorizeNet or PaymentMethodType.Razorpay => "Credit Card",
        PaymentMethodType.PayPal => "PayPal",
        PaymentMethodType.CashOnDelivery => "Cash on Delivery",
        PaymentMethodType.BankTransfer => "Bank Transfer",
        PaymentMethodType.Ebay => "eBay",
        PaymentMethodType.GiftCertificate => "Gift Certificate",
        PaymentMethodType.NoPaymentRequired => "No payment required",
        _ => method.ToString(),
    };

    private static InvoiceAddressDto ToAddress(Address a, string? fallbackName, string? fallbackPhone)
    {
        var name = $"{a.FirstName} {a.LastName}".Trim();
        return new InvoiceAddressDto
        {
            Name = string.IsNullOrWhiteSpace(name) ? fallbackName : name,
            Company = string.IsNullOrWhiteSpace(a.Company) ? null : a.Company.Trim(),
            Line1 = string.IsNullOrWhiteSpace(a.AddressLine1) ? null : a.AddressLine1.Trim(),
            Line2 = string.IsNullOrWhiteSpace(a.AddressLine2) ? null : a.AddressLine2.Trim(),
            CityLine = CityLine(a.City, a.StateProvince, a.PostalCode),
            Phone = string.IsNullOrWhiteSpace(a.PhoneNumber) ? fallbackPhone : a.PhoneNumber.Trim(),
        };
    }

    private static string? CityLine(string? city, string? state, string? zip)
    {
        // A nine-digit ZIP (the ATF directory stores ZIP+4 without the dash) prints as 12345-6789.
        var z = zip?.Trim();
        if (z is { Length: 9 } && z.All(char.IsDigit))
            z = $"{z[..5]}-{z[5..]}";
        var region = $"{state} {z}".Trim();
        var text = string.IsNullOrWhiteSpace(city) ? region : string.IsNullOrWhiteSpace(region) ? city.Trim() : $"{city.Trim()}, {region}";
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
