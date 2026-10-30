using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
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
        var order = await LoadOrderAsync(orderId, ct);
        return BuildInvoice(order);
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
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

    private static byte[] BuildInvoice(Order order)
    {
        string Money(decimal amount) => $"{order.CurrencyCode} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
        var taxLines = ParseTaxBreakdown(order.TaxBreakdownJson);

        // Customer Group pricing is stored as a per-line saving; show the list-price subtotal and itemize
        // the saving so the invoice reconciles (list − group − coupon discount + shipping + tax = total).
        var groupDiscount = order.Lines.Sum(l => l.DiscountAmount);
        var listSubtotal = order.Subtotal + groupDiscount;

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10));

                page.Header().Column(header =>
                {
                    header.Item().Text("INVOICE").FontSize(20).SemiBold();
                    header.Item().Text($"Order {order.OrderNumber}");
                    header.Item().Text($"Placed: {order.PlacedOnUtc.ToString("u", CultureInfo.InvariantCulture)}");
                });

                page.Content().PaddingVertical(12).Column(content =>
                {
                    if (!string.IsNullOrWhiteSpace(order.ContactName))
                        content.Item().Text($"Bill to: {order.ContactName}");

                    // Itemised line table: name, qty, unit price, line total.
                    content.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Item").SemiBold();
                            h.Cell().AlignRight().Text("Qty").SemiBold();
                            h.Cell().AlignRight().Text("Unit Price").SemiBold();
                            h.Cell().AlignRight().Text("Line Total").SemiBold();
                        });

                        foreach (var line in order.Lines)
                        {
                            // The buyer's typed CustomInput values sit under the item name — they are part of
                            // what was ordered, so the invoice has to state them.
                            var customised = CustomAttributes.Describe(line.CustomAttributesJson);
                            table.Cell().Column(cell =>
                            {
                                cell.Item().Text(line.ProductName);
                                if (customised.Length > 0)
                                    cell.Item().Text(customised).FontSize(8).Light();
                            });
                            table.Cell().AlignRight().Text(line.Quantity.ToString(CultureInfo.InvariantCulture));
                            table.Cell().AlignRight().Text(Money(line.UnitPrice));
                            table.Cell().AlignRight().Text(Money(line.LineTotal));
                        }
                    });

                    // Monetary totals, taken verbatim from the order.
                    content.Item().PaddingTop(12).AlignRight().Column(totals =>
                    {
                        totals.Item().Text($"Subtotal: {Money(listSubtotal)}");
                        if (groupDiscount > 0m)
                            totals.Item().Text($"Group discount: -{Money(groupDiscount)}");
                        totals.Item().Text($"Discount: {Money(order.DiscountTotal)}");
                        totals.Item().Text($"Shipping: {Money(order.ShippingTotal)}");
                        totals.Item().Text($"Tax: {Money(order.TaxTotal)}");
                        if (order.PaymentFeeTotal > 0m)
                        {
                            var feeLabel = order.PaymentFeePercent > 0m
                                ? $"Payment fee ({order.PaymentFeePercent.ToString("0.##", CultureInfo.InvariantCulture)}%)"
                                : "Payment fee";
                            totals.Item().Text($"{feeLabel}: {Money(order.PaymentFeeTotal)}");
                        }
                        totals.Item().Text($"Total: {Money(order.TotalAmount)}").FontSize(12).SemiBold();
                    });

                    // Persisted jurisdiction-level tax breakdown (never recalculated).
                    if (taxLines.Count > 0)
                    {
                        content.Item().PaddingTop(12).Text("Tax Breakdown").SemiBold();
                        foreach (var (label, amount) in taxLines)
                            content.Item().Text($"{label}: {Money(amount)}");
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
                    if (!string.IsNullOrWhiteSpace(order.ContactName))
                        header.Item().Text($"Ship to: {order.ContactName}");
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
                            cell.Item().Text(line.ProductName);
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

    /// <summary>
    /// Reads the persisted, immutable tax breakdown as (label, amount) pairs. The stored shape is not
    /// owned by this feature, so parsing is defensive across common property names; an unrecognised or
    /// malformed payload yields no breakdown rather than an error, and nothing is ever recalculated.
    /// </summary>
    private static List<(string Label, decimal Amount)> ParseTaxBreakdown(string? json)
    {
        var result = new List<(string, decimal)>();
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            using var document = JsonDocument.Parse(json);
            var array = FindArray(document.RootElement);
            if (array is not JsonElement arrayElement)
                return result;

            foreach (var element in arrayElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                var label = GetString(element, "jurisdiction", "jurisdictionName", "name", "label", "region")
                            ?? "Tax";
                var amount = GetDecimal(element, "amount", "taxAmount", "tax", "value") ?? 0m;
                var rate = GetDecimal(element, "rate", "taxRate", "percent", "ratePercent");
                if (rate is decimal r && r != 0m)
                    label = $"{label} ({r.ToString("0.##", CultureInfo.InvariantCulture)}%)";

                result.Add((label, amount));
            }
        }
        catch (JsonException)
        {
            // Immutable persisted value in an unexpected shape — omit the breakdown section.
        }

        return result;
    }

    private static JsonElement? FindArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                    return property.Value;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            return property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.ToString(),
                _ => null,
            };
        }

        return null;
    }

    private static decimal? GetDecimal(JsonElement element, params string[] names)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                continue;

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDecimal(out var number))
                return number;

            if (property.Value.ValueKind == JsonValueKind.String
                && decimal.TryParse(property.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }

        return null;
    }
}
