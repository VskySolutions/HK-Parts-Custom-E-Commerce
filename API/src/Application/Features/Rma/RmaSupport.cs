using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>Loading, DTO building and notification helpers shared by the RMA handlers.</summary>
public static class RmaSupport
{
    /// <summary>Loads a tracked RMA with every child collection.</summary>
    public static async Task<Domain.Entities.Rma> LoadAsync(IApplicationDbContext db, Guid id, CancellationToken ct)
        => await db.Rmas
            .Include(r => r.Lines)
            .Include(r => r.History)
            .Include(r => r.Messages)
            .Include(r => r.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Rma), id);

    /// <summary>Loads the RMA's order with lines and ship-to (soft-deleted orders included: the return outlives them).</summary>
    public static async Task<Order> LoadOrderAsync(IApplicationDbContext db, Guid orderId, CancellationToken ct, bool tracked = true)
    {
        var q = db.Orders.IgnoreQueryFilters().Include(o => o.Lines).Include(o => o.ShippingAddress).AsSplitQuery();
        if (!tracked) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);
    }

    /// <summary>Captured payments that can still be refunded, gateway ones first and gift certificates last (RT-68).</summary>
    public static async Task<List<PaymentRecord>> RefundablePaymentsAsync(IApplicationDbContext db, Guid orderId, CancellationToken ct)
    {
        var payments = await db.PaymentRecords
            .Where(p => p.OrderId == orderId
                        && (p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.PartiallyRefunded)
                        && p.Amount > p.RefundedAmount)
            .ToListAsync(ct);
        return payments
            .OrderBy(p => p.Method == PaymentMethodType.GiftCertificate ? 1 : 0)
            .ThenByDescending(p => p.Amount - p.RefundedAmount)
            .ToList();
    }

    /// <summary>Payment methods the system can refund itself; everything else needs a manual refund.</summary>
    public static bool IsAutoRefundable(PaymentMethodType m)
        => m is not (PaymentMethodType.CashOnDelivery or PaymentMethodType.BankTransfer or PaymentMethodType.Ebay or PaymentMethodType.NoPaymentRequired);

    /// <summary>Shipping already given back by other refunded returns on the order (so it is never refunded twice).</summary>
    public static async Task<decimal> ShippingRefundedByOthersAsync(IApplicationDbContext db, Guid orderId, Guid rmaId, CancellationToken ct)
        => await db.Rmas.AsNoTracking()
            .Where(r => r.OrderId == orderId && r.Id != rmaId && r.RefundedOnUtc != null)
            .SumAsync(r => (decimal?)r.ShippingRefund, ct) ?? 0m;

    /// <summary>Computes the breakdown: frozen values after inspection, a live estimate before.</summary>
    public static async Task<RmaRefundBreakdown> BreakdownAsync(
        IApplicationDbContext db, Domain.Entities.Rma rma, Order order, ReturnSettingsDto settings, bool persist, CancellationToken ct)
    {
        if (!persist && RmaWorkflow.Settled.Contains(rma.Status))
        {
            return new RmaRefundBreakdown
            {
                Subtotal = rma.SubtotalRefund,
                Tax = rma.TaxRefund,
                Shipping = rma.ShippingRefund,
                RestockFee = rma.RestockFee,
                LabelDeduction = rma.LabelDeduction,
                Adjustment = rma.Adjustment,
                Total = rma.TotalRefund,
            };
        }
        var claimed = await ReturnEligibility.ClaimedQuantitiesAsync(db, order.Id, rma.Id, ct);
        var shippingByOthers = await ShippingRefundedByOthersAsync(db, order.Id, rma.Id, ct);
        return RmaRefundCalculator.Calculate(rma, order, settings, claimed, shippingByOthers, persist);
    }

    /// <summary>The full detail DTO for <paramref name="rma"/>.</summary>
    public static async Task<RmaDto> ToDtoAsync(
        IApplicationDbContext db, ISettingsService settingsService, Domain.Entities.Rma rma, Order? order, DateTime now, CancellationToken ct)
    {
        order ??= await LoadOrderAsync(db, rma.OrderId, ct, tracked: false);
        var settings = await ReturnSettingsStore.LoadAsync(settingsService, ct);
        var breakdown = await BreakdownAsync(db, rma, order, settings, persist: false, ct);
        var dto = RmaDto.From(rma, order, breakdown, now);
        dto.OrderRefundableBalance = (await RefundablePaymentsAsync(db, order.Id, ct)).Sum(p => p.Amount - p.RefundedAmount);
        if (rma.ReplacementOrderId is Guid rid)
            dto.ReplacementOrderNumber = await db.Orders.IgnoreQueryFilters().Where(o => o.Id == rid).Select(o => o.OrderNumber).FirstOrDefaultAsync(ct);
        return dto;
    }

    /// <summary>The next RMA number for an order: RMA-&lt;order number&gt;-&lt;n&gt; (RT-24).</summary>
    public static async Task<string> NextNumberAsync(IApplicationDbContext db, Order order, CancellationToken ct)
    {
        var count = await db.Rmas.IgnoreQueryFilters().CountAsync(r => r.OrderId == order.Id, ct);
        return $"RMA-{order.OrderNumber}-{count + 1}";
    }

    /// <summary>The customer profile of the signed-in user, or a 401/403.</summary>
    public static async Task<Customer> CurrentCustomerAsync(IApplicationDbContext db, ICurrentUserService current, CancellationToken ct)
    {
        if (current.UserId is not Guid userId)
            throw new UnauthorizedException("Please sign in to manage returns.");
        return await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");
    }

    // ---------------------------------------------------------------- notifications

    public static string Money(decimal v, string currency) => $"{currency} {v.ToString("0.00", CultureInfo.InvariantCulture)}";

    public static string Encode(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

    /// <summary>The lines as an HTML list for emails (names encoded).</summary>
    public static string ItemsHtml(IEnumerable<(string Name, string? Sku, int Qty)> items)
    {
        var sb = new StringBuilder("<ul>");
        foreach (var (name, sku, qty) in items.Where(i => i.Qty > 0))
            sb.Append("<li>").Append(qty).Append(" × ").Append(Encode(name))
              .Append(string.IsNullOrWhiteSpace(sku) ? "" : $" ({Encode(sku)})").Append("</li>");
        return sb.Append("</ul>").ToString();
    }

    /// <summary>Sends a customer-facing return email (best effort; a missing/disabled template sends nothing).</summary>
    public static Task SendCustomerAsync(
        IEmailTemplateSender templates, string key, Order order, Domain.Entities.Rma rma,
        IDictionary<string, string>? extra, CancellationToken ct)
    {
        var vars = BaseVars(order, rma);
        if (extra is not null)
            foreach (var kv in extra) vars[kv.Key] = kv.Value;
        return templates.SendAsync(key, order.ContactEmail ?? string.Empty, order.ContactName, vars, ct);
    }

    /// <summary>RT-81: alerts the staff returns address, when one is configured.</summary>
    public static async Task SendStaffAsync(
        IEmailTemplateSender templates, ReturnSettingsDto settings, Order order, Domain.Entities.Rma rma,
        string subject, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.NotifyEmail))
            return;
        var vars = BaseVars(order, rma);
        vars["staffSubject"] = subject;
        vars["staffMessage"] = Encode(message);
        foreach (var to in settings.NotifyEmail.Split(',', ';').Select(s => s.Trim()).Where(s => s.Length > 0))
            await templates.SendAsync("return.staff-notify", to, null, vars, ct);
    }

    private static Dictionary<string, string> BaseVars(Order order, Domain.Entities.Rma rma) => new()
    {
        ["customerName"] = Encode(string.IsNullOrWhiteSpace(order.ContactName) ? "there" : order.ContactName),
        ["orderNumber"] = Encode(order.OrderNumber),
        ["rmaNumber"] = Encode(rma.RmaNumber),
        ["resolution"] = rma.Resolution.ToString(),
        ["returnItems"] = ItemsHtml(rma.Lines.Select(l => (l.ProductName, l.Sku, l.Quantity))),
    };
}
