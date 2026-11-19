using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Rma;

// =====================================================================================================
// Apply credit — a straight port of the PHP admin's "Apply Credit" (OrderController@creditPost +
// EcommOrder::applyCredit). It edits the order itself; no return (RMA) record is created:
//   1. each returned line: Quantity -= returned, LineTotal recomputed, QuantityRefunded += returned;
//      ticked "Return to stock" lines go back into inventory;
//   2. the order's shipping becomes the amount entered (the difference is credited);
//   3. subtotal, tax and grand total are recalculated (the tax difference is credited);
//   4. the credit is refunded to the original payment (PHP: EcommPayment::creditPayment) — whatever cannot be
//      refunded automatically is reported for a manual refund, as PHP did;
//   5. an order note "Refund applied in the amount of $X / Returned N of SKU: …" is logged.
// =====================================================================================================

/// <summary>One order line on the Apply credit screen.</summary>
public class QuickReturnLineDto
{
    public Guid OrderLineItemId { get; set; }
    public string? Sku { get; set; }
    public string ProductName { get; set; } = string.Empty;
    /// <summary>Units currently on the order line (what can still be credited).</summary>
    public int Quantity { get; set; }
    /// <summary>Units already credited by earlier Apply credit runs.</summary>
    public int QuantityRefunded { get; set; }
    /// <summary>Price per unit charged on the line.</summary>
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public bool RequiresFfl { get; set; }
}

/// <summary>Everything the Apply credit screen needs to show and live-calculate the credit.</summary>
public class QuickReturnFormDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    /// <summary>The order's current shipping — the default for the "Shipping amount" field.</summary>
    public decimal ShippingTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal PaymentFeeTotal { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>What can be refunded automatically to the order's captured payments.</summary>
    public decimal RefundableBalance { get; set; }
    public bool IsPaid { get; set; }
    public bool HasCustomerAccount { get; set; }
    public List<QuickReturnLineDto> Lines { get; set; } = new();
}

public record GetQuickReturnFormQuery(Guid OrderId) : IRequest<QuickReturnFormDto>;

public class GetQuickReturnFormQueryHandler : IRequestHandler<GetQuickReturnFormQuery, QuickReturnFormDto>
{
    private readonly IApplicationDbContext _db;

    public GetQuickReturnFormQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<QuickReturnFormDto> Handle(GetQuickReturnFormQuery request, CancellationToken ct)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        var payments = await RmaSupport.RefundablePaymentsAsync(_db, order.Id, ct);
        return new QuickReturnFormDto
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            CurrencyCode = order.CurrencyCode,
            Subtotal = order.Subtotal,
            DiscountTotal = order.DiscountTotal,
            ShippingTotal = order.ShippingTotal,
            TaxTotal = order.TaxTotal,
            PaymentFeeTotal = order.PaymentFeeTotal,
            TotalAmount = order.TotalAmount,
            RefundableBalance = payments.Where(p => RmaSupport.IsAutoRefundable(p.Method)).Sum(p => p.Amount - p.RefundedAmount),
            IsPaid = OrderCredit.IsPaid(order),
            HasCustomerAccount = order.CustomerId is not null,
            Lines = order.Lines.Select(l => new QuickReturnLineDto
            {
                OrderLineItemId = l.Id,
                Sku = l.Sku,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                QuantityRefunded = l.QuantityRefunded,
                UnitPrice = l.UnitPrice,
                LineTotal = l.LineTotal,
                RequiresFfl = l.IsFfl,
            }).ToList(),
        };
    }
}

/// <summary>One line of the credit: how many units come off the order, and whether they go back on the shelf.</summary>
public record QuickReturnLine(Guid OrderLineItemId, int Quantity, bool ReturnToStock = false);

/// <summary>
/// Apply credit (PHP parity). <see cref="ShippingAmount"/> is the order's NEW shipping amount: it defaults to the
/// current shipping; lowering it credits the difference, raising it keeps back a fee. <see cref="Method"/> picks
/// where the credit goes: the original payment (default, like PHP), store credit, or a manual refund (recorded only).
/// </summary>
public record ApplyQuickReturnCommand(
    Guid OrderId,
    List<QuickReturnLine>? Lines,
    decimal ShippingAmount,
    RmaRefundMethod Method = RmaRefundMethod.OriginalPayment,
    string? ManualReference = null,
    string? Note = null) : IRequest<QuickReturnResultDto>;

public class QuickReturnResultDto
{
    public Guid OrderId { get; set; }
    /// <summary>The credit: returned items + shipping difference + tax difference.</summary>
    public decimal CreditAmount { get; set; }
    /// <summary>The part refunded automatically to the order's payments.</summary>
    public decimal RefundedToPayment { get; set; }
    /// <summary>The part staff still have to refund by hand (no automatic route, or the gateway refused).</summary>
    public decimal ManualRefundDue { get; set; }
    public decimal StoreCreditIssued { get; set; }
    /// <summary>The flash message PHP showed after Apply credit.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>False when the gateway refused the refund (the order was still adjusted).</summary>
    public bool Success { get; set; } = true;
}

public class ApplyQuickReturnCommandValidator : AbstractValidator<ApplyQuickReturnCommand>
{
    public ApplyQuickReturnCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.Quantity).GreaterThanOrEqualTo(0));
        RuleFor(x => x.ShippingAmount).GreaterThanOrEqualTo(0).WithMessage("Shipping amount cannot be negative.");
        RuleFor(x => x.Method).NotEqual(RmaRefundMethod.None);
        RuleFor(x => x.ManualReference).NotEmpty().When(x => x.Method == RmaRefundMethod.Manual)
            .WithMessage("Enter how and where the refund was made (cheque number, gateway portal reference …).");
        RuleFor(x => x.ManualReference).MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
    }
}

public class ApplyQuickReturnCommandHandler : IRequestHandler<ApplyQuickReturnCommand, QuickReturnResultDto>
{
    private readonly ISender _mediator;
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IInventoryService _inventory;
    private readonly IStoreCreditService _storeCredit;
    private readonly IRewardPointsService _points;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;

    public ApplyQuickReturnCommandHandler(
        ISender mediator, IApplicationDbContext db, ISettingsService settings, IInventoryService inventory,
        IStoreCreditService storeCredit, IRewardPointsService points, ICurrentUserService current, IDateTimeProvider clock)
    {
        _mediator = mediator;
        _db = db;
        _settings = settings;
        _inventory = inventory;
        _storeCredit = storeCredit;
        _points = points;
        _current = current;
        _clock = clock;
    }

    public async Task<QuickReturnResultDto> Handle(ApplyQuickReturnCommand request, CancellationToken ct)
    {
        var order = await _db.Orders.Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);
        if (order.IsInquiry)
            throw new ConflictException("This is a quote request, not an order, so there is nothing to credit.");

        var shippingAmount = Math.Round(request.ShippingAmount, 2, MidpointRounding.AwayFromZero);
        var returned = (request.Lines ?? new()).Where(l => l.Quantity > 0).ToList();

        // PHP: "validate we actually need a credit" — a returned quantity, or a changed shipping amount.
        if (returned.Count == 0 && shippingAmount == order.ShippingTotal)
            return new QuickReturnResultDto { OrderId = order.Id, Message = "No credit needed.", Success = false };

        var failures = new List<ValidationFailure>();
        foreach (var r in returned)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == r.OrderLineItemId);
            if (line is null)
                failures.Add(new ValidationFailure("Lines", "One of the items is not on this order."));
            else if (r.Quantity > line.Quantity)
                failures.Add(new ValidationFailure("Lines", $"'{line.ProductName}': you can return at most {line.Quantity}."));
        }
        if (failures.Count > 0)
            throw new ValidationException(failures);
        if (request.Method == RmaRefundMethod.StoreCredit && order.CustomerId is null)
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.Method), "Store credit needs a customer account; this order was placed as a guest.") });

        // ---- Work out the new order totals and the credit (EcommOrder::applyCredit) ----------------------------
        var oldSubtotal = order.Subtotal;
        var oldDiscount = order.DiscountTotal;
        var oldShipping = order.ShippingTotal;
        var oldTax = order.TaxTotal;

        var newSubtotal = order.Lines.Sum(l =>
        {
            var q = returned.Where(r => r.OrderLineItemId == l.Id).Sum(r => r.Quantity);
            return l.UnitPrice * (l.Quantity - q);
        });
        newSubtotal = OrderCredit.Round(newSubtotal);
        // Order-level discounts (coupon, points) shrink with the goods they were taken off — PHP credited the
        // discounted price of each unit.
        var newDiscount = oldSubtotal > 0 ? OrderCredit.Round(oldDiscount * newSubtotal / oldSubtotal) : oldDiscount;
        // Tax recalculated on the reduced goods (PHP: EcommTax::getTaxAmount on the new subtotal). Pro-rated from the
        // tax charged at placement so the jurisdiction rates of the original order are kept.
        var oldBase = oldSubtotal - oldDiscount;
        var newBase = newSubtotal - newDiscount;
        var newTax = oldBase > 0 ? OrderCredit.Round(oldTax * newBase / oldBase) : oldTax;

        var credit = OrderCredit.Round(
            (oldSubtotal - newSubtotal) - (oldDiscount - newDiscount) + (oldShipping - shippingAmount) + (oldTax - newTax));
        if (credit < 0)
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.ShippingAmount),
                $"The credit would be negative ({OrderCredit.Money(credit, order.CurrencyCode)}). Lower the shipping amount.") });
        var refundable = OrderCredit.IsPaid(order)
            ? (await RmaSupport.RefundablePaymentsAsync(_db, order.Id, ct)).Sum(p => p.Amount - p.RefundedAmount)
            : 0m;

        // ---- Update the order lines, restock, and the order totals ------------------------------------------------
        var settings = await ReturnSettingsStore.LoadAsync(_settings, ct);
        var storeId = order.AssignedStoreId ?? settings.DefaultRestockStoreId;
        var comment = new List<string>();
        foreach (var r in returned)
        {
            var line = order.Lines.First(l => l.Id == r.OrderLineItemId);
            line.Quantity -= r.Quantity;
            line.QuantityRefunded += r.Quantity;
            line.LineTotal = OrderCredit.Round(line.UnitPrice * line.Quantity);
            line.DiscountAmount = OrderCredit.Round(Math.Max(0m, line.OriginalUnitPrice - line.UnitPrice) * line.Quantity);
            line.QuantityShipped = Math.Min(line.QuantityShipped, line.Quantity);
            comment.Add($"Returned {r.Quantity} of SKU: {line.Sku ?? line.ProductName}");

            if (r.ReturnToStock)
                comment.Add(await RestockAsync(line, r.Quantity, storeId, order.OrderNumber, ct));
        }

        if (shippingAmount != oldShipping)
            comment.Add($"Shipping changed from {OrderCredit.Money(oldShipping, order.CurrencyCode)} to {OrderCredit.Money(shippingAmount, order.CurrencyCode)}");

        order.Subtotal = newSubtotal;
        order.DiscountTotal = newDiscount;
        order.ShippingTotal = shippingAmount;
        order.TaxTotal = newTax;
        order.TotalAmount = OrderCredit.Round(order.TotalAmount - credit);

        var now = _clock.UtcNow;
        var by = _current.Email ?? "an administrator";
        var header = $"Refund applied in the amount of {OrderCredit.Money(credit, order.CurrencyCode)} by {by}.";
        if (!string.IsNullOrWhiteSpace(request.Note))
            comment.Add("Note: " + request.Note.Trim());
        var history = new OrderStatusHistory
        {
            OrderId = order.Id,
            FromStatus = order.Status,
            ToStatus = order.Status,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
            Note = OrderCredit.Truncate(header + " " + string.Join(". ", comment.Where(c => c.Length > 0)) + ".", 1000),
        };
        _db.OrderStatusHistory.Add(history);
        await _db.SaveChangesAsync(ct);

        // ---- Refund (OrderController@creditPost) -------------------------------------------------------------------
        var result = new QuickReturnResultDto { OrderId = order.Id, CreditAmount = credit };
        if (credit <= 0)
        {
            result.Message = "Order updated. No credit needed.";
            return result;
        }
        if (!OrderCredit.IsPaid(order) && request.Method == RmaRefundMethod.OriginalPayment)
        {
            result.Message = $"Order updated with a credit of {OrderCredit.Money(credit, order.CurrencyCode)}. The order is not paid, so nothing was refunded.";
            return result;
        }

        switch (request.Method)
        {
            case RmaRefundMethod.StoreCredit:
                await _storeCredit.IssueAsync(order.CustomerId!.Value, credit, order.CurrencyCode,
                    $"Credit applied to order {order.OrderNumber}", null, order.Id, ct);
                result.StoreCreditIssued = credit;
                result.Message = $"Credit of {OrderCredit.Money(credit, order.CurrencyCode)} issued as store credit.";
                break;

            case RmaRefundMethod.Manual:
                result.ManualRefundDue = 0m;
                result.Message = $"Credit of {OrderCredit.Money(credit, order.CurrencyCode)} recorded as a manual refund ({request.ManualReference!.Trim()}).";
                break;

            default:
                await RefundToPaymentsAsync(order, credit, result, ct);
                break;
        }

        history.Note = OrderCredit.Truncate(history.Note + " " + result.Message, 1000);
        await _db.SaveChangesAsync(ct);

        // Loyalty points earned by the order are clawed back once its payment is fully refunded.
        if (order.CustomerId is Guid cid && order.PaymentStatus is PaymentStatus.Refunded)
            await _points.DeductForRefundAsync(cid, order.Id, ct);

        return result;
    }

    /// <summary>
    /// Refunds the credit to the order's captured payments, card/gateway first (PHP: EcommPayment::creditPayment).
    /// Whatever has no automatic route (cash on delivery, bank transfer …) or is refused by the gateway is reported
    /// as a manual refund, as PHP did ("You will need to manually refund …"). The order adjustment always stands.
    /// </summary>
    private async Task RefundToPaymentsAsync(Order order, decimal credit, QuickReturnResultDto result, CancellationToken ct)
    {
        var remaining = credit;
        var payments = (await RmaSupport.RefundablePaymentsAsync(_db, order.Id, ct))
            .Where(p => RmaSupport.IsAutoRefundable(p.Method)).ToList();
        string? error = null;
        foreach (var payment in payments)
        {
            if (remaining <= 0) break;
            var part = Math.Min(remaining, payment.Amount - payment.RefundedAmount);
            try
            {
                await _mediator.Send(new Payments.RefundPaymentCommand(payment.Id, part, $"Credit applied to order {order.OrderNumber}"), ct);
            }
            catch (Exception ex) when (ex is ConflictException or ValidationException or NotFoundException or ForbiddenAccessException)
            {
                error = ex is ValidationException ve && ve.Errors.Count > 0 ? string.Join(" ", ve.Errors.SelectMany(e => e.Value)) : ex.Message;
                break;
            }
            remaining -= part;
            result.RefundedToPayment += part;
        }

        result.ManualRefundDue = OrderCredit.Round(remaining);
        var money = (decimal v) => OrderCredit.Money(v, order.CurrencyCode);
        if (error is not null)
        {
            result.Success = false;
            result.Message = $"Credit applied, but the refund could not be processed automatically: {error} " +
                             $"Refund {money(result.ManualRefundDue)} manually through your gateway.";
        }
        else if (result.ManualRefundDue > 0)
        {
            result.Message = (result.RefundedToPayment > 0 ? $"{money(result.RefundedToPayment)} refunded to the original payment. " : "Credit applied successfully. ") +
                             $"You will need to manually refund {money(result.ManualRefundDue)}.";
        }
        else
        {
            result.Message = $"Credit applied successfully. {money(result.RefundedToPayment)} refunded to the original payment.";
        }
    }

    /// <summary>Puts returned units back into the order's store (PHP: qty_in_stock += returned).</summary>
    private async Task<string> RestockAsync(OrderLineItem line, int quantity, Guid? storeId, string orderNumber, CancellationToken ct)
    {
        if (storeId is not Guid sid)
            return $"{line.ProductName}: not restocked — the order has no store and no default restock store is set";
        if (!await _db.Products.AnyAsync(p => p.Id == line.ProductId, ct))
            return $"{line.ProductName}: not restocked — the product no longer exists";

        var newQty = await _inventory.AdjustStockAsync(line.ProductId, line.ProductVariantId, sid, quantity, ct);
        _db.InventoryAuditLogs.Add(new InventoryAuditLog
        {
            ProductId = line.ProductId,
            ProductVariantId = line.ProductVariantId,
            StoreId = sid,
            OldQty = Math.Max(0, newQty - quantity),
            NewQty = Math.Max(0, newQty),
            Action = InventoryAuditAction.ReturnRestock,
            Notes = OrderCredit.Truncate($"Order {orderNumber}: credit applied, restocked {quantity}", 500),
            Source = InventoryScanSource.Manual,
            ClientIp = _current.IpAddress,
        });
        return $"restocked {quantity} (stock now {newQty})";
    }
}

internal static class OrderCredit
{
    public static bool IsPaid(Order o) => o.PaymentStatus is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded;
    public static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    public static string Money(decimal v, string currency) => RmaSupport.Money(v, currency);
    public static string Truncate(string v, int max) => v.Length <= max ? v : v[..max];
}
