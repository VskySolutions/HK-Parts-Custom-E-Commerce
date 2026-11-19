using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>One order line as the return form sees it.</summary>
public class ReturnableLineDto
{
    public Guid OrderLineItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int QuantityOrdered { get; set; }
    /// <summary>Units already on other (non-rejected / cancelled / expired) returns.</summary>
    public int QuantityOnReturns { get; set; }
    /// <summary>RT-12: what can still be requested.</summary>
    public int QuantityReturnable { get; set; }
    /// <summary>What the customer actually paid per unit, after order-level discounts (RT-56).</summary>
    public decimal UnitPaid { get; set; }
    public bool Eligible { get; set; }
    /// <summary>Why the line cannot be returned ("Final sale", "Already returned", …).</summary>
    public string? IneligibleReason { get; set; }
    /// <summary>RT-14: a firearm — staff-only return through a licensed dealer.</summary>
    public bool RequiresFfl { get; set; }
    /// <summary>RT-15: hazmat / freight — cannot go back by parcel carrier.</summary>
    public bool ShipRestricted { get; set; }
}

public class ReturnEligibilityDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    /// <summary>At least one line can be returned now.</summary>
    public bool Eligible { get; set; }
    /// <summary>Order-level reason when nothing can be returned (window closed, eBay order, …).</summary>
    public string? Reason { get; set; }
    public DateTime? WindowClosesOnUtc { get; set; }
    public bool RequiresFfl { get; set; }
    public List<ReturnableLineDto> Lines { get; set; } = new();
}

/// <summary>
/// Return eligibility (order-return-requirements section 5). The same rules drive the storefront form, customer
/// submit and admin create; staff may bypass the policy checks (RT-17) but never the quantity check (RT-12).
/// </summary>
public static class ReturnEligibility
{
    /// <summary>Reads the order (with lines) and evaluates it. <paramref name="excludeRmaId"/> skips an RMA being edited.</summary>
    public static async Task<ReturnEligibilityDto> EvaluateAsync(
        IApplicationDbContext db, Order order, ReturnSettingsDto settings, DateTime now, bool forStaff,
        Guid? excludeRmaId, CancellationToken ct)
    {
        var dto = new ReturnEligibilityDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CurrencyCode = order.CurrencyCode,
        };

        // ---- order-level rules ----
        string? orderReason = null;
        var hardBlock = false;
        if (!string.IsNullOrEmpty(order.EbayOrderId))
        {
            orderReason = "This order was placed on eBay. Please request the return through eBay.";
            hardBlock = true; // RT-93: never returned here, staff included
        }
        else if (order.IsInquiry)
        {
            orderReason = "This is a quote request, not an order.";
            hardBlock = true;
        }
        else if (!forStaff && !settings.Enabled)
        {
            orderReason = "Online returns are not available. Please contact us.";
        }
        else if (!settings.EligibleStatuses.Contains(order.Status))
        {
            orderReason = order.Status is OrderStatus.Cancelled
                ? "This order was cancelled."
                : "This order has not shipped yet. You can cancel unshipped items instead.";
            if (order.Status is OrderStatus.Cancelled) hardBlock = true;
        }

        var shippedOn = order.ShippedOnUtc ?? order.DeliveredOnUtc ?? order.PlacedOnUtc;
        dto.WindowClosesOnUtc = shippedOn.AddDays(Math.Max(1, settings.WindowDays));
        if (orderReason is null && now > dto.WindowClosesOnUtc)
            orderReason = $"The return window closed on {dto.WindowClosesOnUtc:MMM d, yyyy}.";

        // Staff override every policy rule except the hard blocks (RT-17).
        var policyBlocked = orderReason is not null && (hardBlock || !forStaff);

        // ---- quantities already claimed by other returns ----
        var claimed = await ClaimedQuantitiesAsync(db, order.Id, excludeRmaId, ct);

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsReturnable, p.IsFflRequired, p.IsHazardous, p.FreightClass, p.ProductType })
            .ToDictionaryAsync(p => p.Id, ct);

        var unitPaid = UnitPaidByLine(order);

        foreach (var line in order.Lines.OrderBy(l => l.ProductName))
        {
            products.TryGetValue(line.ProductId, out var p);
            var onReturns = claimed.GetValueOrDefault(line.Id);
            var returnable = Math.Max(0, line.Quantity - onReturns);
            var row = new ReturnableLineDto
            {
                OrderLineItemId = line.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                QuantityOrdered = line.Quantity,
                QuantityOnReturns = onReturns,
                QuantityReturnable = returnable,
                UnitPaid = unitPaid.GetValueOrDefault(line.Id),
                RequiresFfl = p?.IsFflRequired ?? false,
                ShipRestricted = p is not null && (p.IsHazardous || !string.IsNullOrWhiteSpace(p.FreightClass)),
            };

            string? lineReason = null;
            if (returnable <= 0)
                lineReason = onReturns > 0 ? "Already returned or on an open return." : "Nothing left to return.";
            else if (p is not null && p.ProductType == ProductType.GiftCard)
                lineReason = "Gift certificates cannot be returned online."; // RT-18 — staff can still override
            else if (p is not null && !p.IsReturnable)
                lineReason = "Final sale — this item cannot be returned.";

            // Quantity is never overridable; final-sale / gift-card lines are, for staff.
            var lineBlocked = returnable <= 0 || (lineReason is not null && !forStaff);
            row.Eligible = !policyBlocked && !lineBlocked;
            row.IneligibleReason = lineReason ?? (policyBlocked ? orderReason : null);
            dto.Lines.Add(row);
        }

        dto.Eligible = dto.Lines.Any(l => l.Eligible);
        dto.RequiresFfl = dto.Lines.Any(l => l.RequiresFfl);
        dto.Reason = dto.Eligible ? (forStaff ? orderReason : null) : orderReason ?? "Nothing on this order can be returned.";
        return dto;
    }

    /// <summary>
    /// Units of each order line claimed by returns that are not rejected / cancelled / expired: the requested
    /// quantity until inspection, then the accepted quantity (what actually came back).
    /// </summary>
    public static async Task<Dictionary<Guid, int>> ClaimedQuantitiesAsync(IApplicationDbContext db, Guid orderId, Guid? excludeRmaId, CancellationToken ct)
    {
        var rows = await db.RmaLineItems.AsNoTracking()
            .Where(l => l.Rma!.OrderId == orderId
                        && !RmaWorkflow.Released.Contains(l.Rma.Status)
                        && (excludeRmaId == null || l.RmaId != excludeRmaId))
            .Select(l => new { l.OrderLineItemId, l.Quantity, l.QuantityAccepted, l.Rma!.Status })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.OrderLineItemId)
            .ToDictionary(g => g.Key, g => g.Sum(r => RmaWorkflow.Settled.Contains(r.Status) ? r.QuantityAccepted : r.Quantity));
    }

    /// <summary>
    /// What the customer paid per unit of each line: the merchandise part of the order total (total − shipping −
    /// tax − payment fee) spread over the lines by line value, so coupons and points are pro-rated (RT-56) and a
    /// refund never returns more than was paid for the line.
    /// </summary>
    public static Dictionary<Guid, decimal> UnitPaidByLine(Order order)
    {
        var linesTotal = order.Lines.Sum(l => l.LineTotal);
        var merchandisePaid = order.TotalAmount - order.ShippingTotal - order.TaxTotal - order.PaymentFeeTotal;
        if (merchandisePaid < 0) merchandisePaid = 0;
        // When nothing was discounted at order level this equals the line totals exactly.
        var factor = linesTotal > 0 ? Math.Min(1m, merchandisePaid / linesTotal) : 0m;

        return order.Lines.ToDictionary(
            l => l.Id,
            l => l.Quantity > 0 ? Math.Round(l.LineTotal * factor / l.Quantity, 2, MidpointRounding.AwayFromZero) : 0m);
    }
}
