using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Documents;

/// <summary>
/// QuestPDF-based renderer for order invoices and packing slips (WO-47). All financial figures are
/// taken verbatim from the persisted <see cref="Order"/> — the tax breakdown is parsed from the
/// immutable <see cref="Order.TaxBreakdownJson"/> and never recalculated (AC-ORD-003.4).
/// </summary>
public class OrderDocumentService : IOrderDocumentService
{
    static OrderDocumentService()
    {
        // QuestPDF requires a license be declared before any document is generated.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private readonly IApplicationDbContext _db;

    public OrderDocumentService(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> GenerateInvoiceAsync(Guid orderId, CancellationToken ct)
    {
        // Same data as the on-screen invoice page (GET /api/admin/orders/{id}/invoice/view).
        var invoice = await OrderInvoiceBuilder.BuildAsync(_db, orderId, ct);
        return BuildInvoice(invoice);
    }

    public async Task<byte[]> GeneratePackingSlipAsync(Guid orderId, CancellationToken ct)
    {
        var order = await LoadOrderAsync(orderId, ct);
        return BuildPackingSlip(order);
    }

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken ct)
        => await _db.Orders
            .Include(o => o.ShippingAddress)
            .AsNoTracking()
            .Include(o => o.Lines)
            .Include(o => o.Shippings)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

    private const string Ink = "#333333";
    private const string Muted = "#777777";
    private const string Rule = "#DDDDDD";
    private const string PanelHeading = "#F5F5F5";

    /// <summary>The PHP admin's invoice layout: title, order number/date, billing + shipping panels, line table, totals.</summary>
    private static byte[] BuildInvoice(InvoiceDto inv)
    {
        string Money(decimal amount) => OrderInvoiceBuilder.Money(amount, inv.CurrencyCode);

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Ink).FontFamily(Fonts.TimesNewRoman));

                page.Content().Column(col =>
                {
                    col.Spacing(10);
                    col.Item().AlignRight().Text("INVOICE").FontSize(26);

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text($"Order #: {inv.OrderNumber}").FontSize(14).FontColor(Muted);
                        row.RelativeItem().AlignRight().Text($"Order Date: {inv.OrderDateText}").FontSize(14).FontColor(Muted);
                    });

                    col.Item().Row(row =>
                    {
                        row.RelativeItem(4).Element(e => Panel(e, "Billing Address", body =>
                        {
                            AddressLines(body, inv.Billing);
                            if (!string.IsNullOrWhiteSpace(inv.PaymentMethod))
                                body.Item().PaddingTop(10).Text($"Payment Method: {inv.PaymentMethod}");
                        }));
                        row.RelativeItem(2);
                        row.RelativeItem(4).Element(e => Panel(e, inv.IsPickup ? "Pickup" : inv.ShipsToFflDealer ? "Shipping Address (FFL Dealer)" : "Shipping Address", body =>
                        {
                            if (inv.Shipping is not null)
                                AddressLines(body, inv.Shipping);
                            if (inv.ShipsToFflDealer && !string.IsNullOrWhiteSpace(inv.FflNumber))
                                body.Item().Text($"FFL Number: {inv.FflNumber}");
                            if (!string.IsNullOrWhiteSpace(inv.ShippingMethod))
                                body.Item().PaddingTop(inv.Shipping is null ? 0 : 10).Text($"Shipping Method: {inv.ShippingMethod}");
                            if (inv.FflDealer is not null)
                            {
                                body.Item().PaddingTop(10).Text("FFL items ship to:").SemiBold();
                                AddressLines(body, inv.FflDealer);
                                if (!string.IsNullOrWhiteSpace(inv.FflNumber))
                                    body.Item().Text($"FFL Number: {inv.FflNumber}");
                                if (!string.IsNullOrWhiteSpace(inv.FflShippingMethod))
                                    body.Item().Text($"Shipping Method: {inv.FflShippingMethod}");
                            }
                        }));
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(4.2f);
                            c.RelativeColumn(1f);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn(2f);
                        });

                        static IContainer Cell(IContainer c) => c.Border(0.75f).BorderColor(Rule).PaddingVertical(6).PaddingHorizontal(5);
                        table.Header(h =>
                        {
                            foreach (var title in new[] { "SKU", "Name", "Qty", "Price", "Sub Total" })
                                h.Cell().Element(Cell).Text(title).FontSize(11);
                        });

                        foreach (var line in inv.Lines)
                        {
                            table.Cell().Element(Cell).Text(line.Sku ?? string.Empty);
                            table.Cell().Element(Cell).Column(c =>
                            {
                                c.Item().Text(line.Name);
                                if (!string.IsNullOrWhiteSpace(line.Options))
                                    c.Item().Text(line.Options).FontSize(8).FontColor(Muted);
                            });
                            table.Cell().Element(Cell).AlignRight().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Element(Cell).AlignRight().Text(Money(line.Price));
                            table.Cell().Element(Cell).AlignRight().Text(Money(line.SubTotal));
                        }
                    });

                    col.Item().AlignRight().Width(240).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn(2);
                        });
                        void Total(string label, string value)
                        {
                            t.Cell().AlignRight().PaddingRight(8).Text(label).Bold();
                            t.Cell().AlignRight().Text(value).Bold();
                        }

                        Total("Sub Total:", Money(inv.SubTotal));
                        if (inv.Discount > 0m)
                            Total("Discount:", Money(-inv.Discount));
                        Total("Shipping:", Money(inv.ShippingTotal));
                        Total("TAX:", Money(inv.Tax));
                        if (inv.PaymentFee > 0m)
                            Total("Payment Fee:", Money(inv.PaymentFee));
                        Total("Total:", Money(inv.Total));
                        Total("Payment Applied:", Money(inv.PaymentApplied));
                        if (inv.Refunded > 0m)
                            Total("Refunded:", Money(inv.Refunded));
                        Total("Amount Due:", Money(inv.AmountDue));
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8).FontColor(Muted));
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void Panel(IContainer container, string title, Action<ColumnDescriptor> body)
    {
        container.Border(0.75f).BorderColor(Rule).Column(panel =>
        {
            panel.Item().Background(PanelHeading).BorderBottom(0.75f).BorderColor(Rule)
                .PaddingVertical(9).PaddingHorizontal(10).Text(title).FontSize(12);
            panel.Item().PaddingVertical(10).PaddingHorizontal(10).Column(body);
        });
    }

    private static void AddressLines(ColumnDescriptor body, InvoiceAddressDto a)
    {
        foreach (var line in new[] { a.Name, a.Company, a.Line1, a.Line2, a.CityLine, a.Phone })
            if (!string.IsNullOrWhiteSpace(line))
                body.Item().Text(line);
    }

    private static byte[] BuildPackingSlip(Order order)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Text("PACKING SLIP").FontSize(20).SemiBold();
                    header.Item().Text($"Order {order.OrderNumber}");
                    var fflRow = order.Shippings.FirstOrDefault(s => s.ShipToType == Domain.Enums.ShipToType.FflDealer);
                    var fflOnly = fflRow is not null && order.Lines.All(l => l.IsFfl);
                    if (!fflOnly && !string.IsNullOrWhiteSpace(order.ContactName))
                        header.Item().Text(fflRow is null ? $"Ship to: {order.ContactName}" : $"Non-FFL items ship to: {order.ContactName}");
                    if (fflRow is not null)
                    {
                        // The firearms go to the licensed dealer, never to the buyer.
                        header.Item().PaddingTop(4).Text("FFL items ship to the FFL dealer:").SemiBold();
                        header.Item().Text($"FFL {fflRow.FflDealerName}");
                        if (!string.IsNullOrWhiteSpace(fflRow.FflDealerStreet))
                            header.Item().Text($"{fflRow.FflDealerStreet}, {fflRow.FflDealerCity}, {fflRow.FflDealerState} {fflRow.FflDealerZip}");
                        if (!string.IsNullOrWhiteSpace(fflRow.FflNumber))
                            header.Item().Text($"FFL Number: {fflRow.FflNumber}");
                    }
                });

                page.Content().PaddingVertical(12).Table(table =>
                {
                    // Items and quantities only — deliberately no prices.
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(6);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Item").SemiBold();
                        h.Cell().Text("SKU").SemiBold();
                        h.Cell().AlignRight().Text("Qty").SemiBold();
                    });

                    foreach (var line in order.Lines)
                    {
                        // The picker/packer needs the buyer's typed values (engraving, gift note, …) too.
                        var customised = CustomAttributes.Describe(line.CustomAttributesJson);
                        table.Cell().Column(cell =>
                        {
                            cell.Item().Text(line.IsFfl ? $"{line.ProductName} (FFL: ship to dealer)" : line.ProductName);
                            if (customised.Length > 0)
                                cell.Item().Text(customised).FontSize(8).Light();
                        });
                        table.Cell().Text(line.Sku ?? "-");
                        table.Cell().AlignRight().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();
    }
}
