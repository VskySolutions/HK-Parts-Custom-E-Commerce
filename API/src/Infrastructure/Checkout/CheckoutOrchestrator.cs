using System.Globalization;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Checkout;
using HKPartsEcommerce.Application.Features.OrderRouting;
using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Checkout;

/// <summary>
/// The checkout capstone (WO-30, REQ-CHK-003). Wires the already-built commerce services into the
/// canonical checkout sequence: load cart → route to a store → enforce guest-ordering policy → quote
/// shipping → evaluate discounts/coupons → calculate tax → (on placement) create the order, authorize
/// payment, and finalize. Pricing math is shared between <see cref="QuoteAsync"/> and
/// <see cref="PlaceAsync"/> via <see cref="BuildAsync"/>, so a quote and its placement agree.
/// </summary>
public class CheckoutOrchestrator : ICheckoutOrchestrator
{
    private readonly IApplicationDbContext _db;
    private readonly IOrderRoutingEngine _routing;
    private readonly IShippingRateService _shipping;
    private readonly IShippingOptionSelector _shippingSelector;
    private readonly IDiscountService _discounts;
    private readonly ICouponService _coupons;
    private readonly ITaxCalculationService _tax;
    private readonly IPaymentGatewayRouter _payments;
    private readonly ICurrentUserService _current;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _templates;
    private readonly IPublisher _publisher;
    private readonly ICustomerGroupService _customerGroups;
    private readonly IInventoryService _inventory;
    private readonly IRewardPointsService _points;
    private readonly ICommerceModeService _commerce;

    public CheckoutOrchestrator(
        IApplicationDbContext db,
        IOrderRoutingEngine routing,
        IShippingRateService shipping,
        IShippingOptionSelector shippingSelector,
        IDiscountService discounts,
        ICouponService coupons,
        ITaxCalculationService tax,
        IPaymentGatewayRouter payments,
        ICurrentUserService current,
        IDateTimeProvider clock,
        IEmailTemplateSender templates,
        IPublisher publisher,
        ICustomerGroupService customerGroups,
        IInventoryService inventory,
        IRewardPointsService points,
        ICommerceModeService commerce)
    {
        _db = db;
        _routing = routing;
        _shipping = shipping;
        _shippingSelector = shippingSelector;
        _discounts = discounts;
        _coupons = coupons;
        _tax = tax;
        _payments = payments;
        _current = current;
        _clock = clock;
        _templates = templates;
        _publisher = publisher;
        _customerGroups = customerGroups;
        _inventory = inventory;
        _points = points;
        _commerce = commerce;
    }

    public async Task<CheckoutQuote> QuoteAsync(CheckoutQuoteRequest req, CancellationToken ct)
    {
        var priced = await BuildAsync(
            req.CartId, req.SessionId, req.ShipTo, req.ShippingMethodId,
            requestCouponCode: null, forPlacement: false, req.PickupInStore, req.PickupStoreId, ct);

        var quote = new CheckoutQuote
        {
            Subtotal = priced.Subtotal,
            BaseSubtotal = priced.BaseSubtotal,
            GroupDiscountTotal = priced.GroupDiscountTotal,
            GroupDiscountName = priced.GroupDiscountName,
            Discounts = priced.Discounts,
            DiscountTotal = priced.DiscountTotal,
            ShippingOptions = priced.ShippingOptions.ToList(),
            ShippingTotal = priced.ShippingTotal,
            Tax = priced.Tax,
            TaxTotal = priced.TaxTotal,
            Total = priced.Total,
            AssignedStoreId = priced.Routing.AssignedStoreId,
            IsRoutable = priced.Routing.IsRouted,
            GuestOrderingAllowed = priced.GuestOrderingAllowed,
            IsInquiry = priced.IsInquiry,
        };

        // An inquiry takes no payment, so no method is offered and none may be selected — the storefront
        // renders the request form instead of the payment step.
        // Only a routed order has a fulfilling store, and so a concrete set of payment methods to offer.
        if (priced.Store is not null && !priced.IsInquiry)
            quote.AvailablePaymentMethods = (await AvailablePaymentMethodsAsync(priced.Store, ct))
                .Select(m => new PaymentMethodOption { Method = m.Method.ToString(), IsProduction = m.IsProduction, FeePercent = m.FeePercent })
                .ToList();

        // The active tax provider (for the storefront's "calculated via …" indicator); defaults to FlatRate
        // when no configuration row exists, mirroring TaxCalculationService.
        quote.TaxProvider = (await _db.TaxProviderConfigurations
            .AsNoTracking()
            .Select(c => (TaxProviderType?)c.ActiveProvider)
            .FirstOrDefaultAsync(ct) ?? TaxProviderType.FlatRate).ToString();

        return quote;
    }

    /// <summary>
    /// Payment methods offered for an order fulfilled by <paramref name="store"/>: the active/enabled +
    /// credential-configured gateways (<see cref="IPaymentGatewayRouter.AvailableMethodsAsync"/>), minus the
    /// manual methods this store switches off (<see cref="Domain.Entities.Store.CashOnDeliveryEnabled"/>,
    /// <see cref="Domain.Entities.Store.BankTransferEnabled"/>). Both settle at the store rather than through
    /// a gateway, so whether they can be honoured is the fulfilling store's call, not the platform's.
    /// </summary>
    private async Task<IReadOnlyList<PaymentMethodAvailability>> AvailablePaymentMethodsAsync(Domain.Entities.Store store, CancellationToken ct)
    {
        var methods = await _payments.AvailableMethodsAsync(ct);
        return methods.Where(m => m.Method switch
        {
            PaymentMethodType.CashOnDelivery => store.CashOnDeliveryEnabled,
            PaymentMethodType.BankTransfer => store.BankTransferEnabled,
            _ => true,
        }).ToList();
    }

    public async Task<CheckoutResult> PlaceAsync(PlaceCheckoutRequest req, CancellationToken ct)
    {
        var priced = await BuildAsync(
            req.CartId, req.SessionId, req.ShipTo, req.SelectedShippingMethodId,
            requestCouponCode: req.CouponCode, forPlacement: true, req.PickupInStore, req.PickupStoreId, ct);

        // Inquiry carts never reach a gateway (REQ-INQ-001). The storefront already renders the request
        // form instead of the payment step, but the switch is enforced here so a crafted request cannot
        // pay for a quote-only cart — or pay at all in an inquiry-only tenant.
        if (priced.IsInquiry)
            throw new ConflictException(
                "This store does not accept online payment for these items. Please submit an inquiry instead.");

        var now = _clock.UtcNow;
        var storeId = priced.Routing.AssignedStoreId!.Value; // non-null: placement short-circuits when unrouted.

        // Loyalty points staged on the cart reduce the payable total (WO-27). Clamp the discount so it can
        // never exceed the priced total; the balance itself is debited once, in FinalizePlacedOrderAsync.
        var pointsRedeemed = priced.Cart.PointsApplied > 0 ? priced.Cart.PointsApplied : 0;
        var pointsDiscount = pointsRedeemed > 0 ? Math.Min(priced.Cart.PointsDiscountAmount, priced.Total) : 0m;
        var netTotal = priced.Total - pointsDiscount;

        // Enforce the store's payment-method availability (e.g. COD switched off, or a deactivated gateway)
        // server-side — the storefront only ever offers these, but never trust the client.
        var availableMethods = priced.Store is null
            ? (IReadOnlyList<PaymentMethodAvailability>)Array.Empty<PaymentMethodAvailability>()
            : await AvailablePaymentMethodsAsync(priced.Store, ct);
        if (priced.Store is not null && !availableMethods.Any(m => m.Method == req.PaymentMethod))
            throw new ConflictException("The selected payment method is not available for this order.");

        // Payment transaction fee: the chosen gateway's configured fee (% of the order total) is added as an
        // additional charge the buyer pays (WO-fee). Resolved authoritatively here from the active credential —
        // never from the client — and 0 when the method has no fee. Applied to the pre-fee grand total.
        var feePercent = availableMethods.FirstOrDefault(m => m.Method == req.PaymentMethod)?.FeePercent ?? 0m;
        var paymentFee = decimal.Round(netTotal * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var grandTotal = netTotal + paymentFee;

        // Resolve the customer only when the caller is an authenticated customer; guests place a null-customer order.
        Guid? customerId = null;
        if (_current.UserId is Guid userId)
        {
            customerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
        }

        // 8a. Create the order + its shared delivery Address + line snapshots + the initial status marker.
        var order = new Order
        {
            OrderNumber = $"ORD-{now:yyyyMMddHHmmssfff}",
            Status = OrderStatus.Pending,
            CustomerId = customerId,
            ShippingAddress = new Address
            {
                FirstName = req.ShipTo.FirstName,
                LastName = req.ShipTo.LastName,
                Email = req.ShipTo.Email,
                PhoneNumber = req.ShipTo.PhoneNumber,
                Latitude = req.ShipTo.Latitude,
                Longitude = req.ShipTo.Longitude,
                CountryCode = req.ShipTo.CountryCode ?? string.Empty,
                StateProvince = req.ShipTo.Region,
                PostalCode = req.ShipTo.PostalCode ?? string.Empty,
                AddressLine1 = req.ShipTo.Line1 ?? string.Empty,
                AddressLine2 = req.ShipTo.Line2,
                Landmark = req.ShipTo.Landmark,
                City = req.ShipTo.City ?? string.Empty,
            },
            AssignedStoreId = storeId,
            IsPickup = priced.IsPickup,
            PlacedOnUtc = now,
            RoutedOnUtc = now,
            CurrencyCode = priced.Cart.CurrencyCode,
            Subtotal = priced.Subtotal,
            DiscountTotal = priced.DiscountTotal + pointsDiscount,
            ShippingTotal = priced.ShippingTotal,
            TaxTotal = priced.TaxTotal,
            PaymentFeePercent = feePercent,
            PaymentFeeTotal = paymentFee,
            TotalAmount = grandTotal,
            PointsRedeemed = pointsRedeemed,
            PointsDiscountAmount = pointsDiscount,
            // Immutable jurisdiction-level tax breakdown, stored verbatim and never recalculated.
            TaxBreakdownJson = JsonSerializer.Serialize(priced.Tax),
            TaxFlaggedForReview = priced.Tax.FallbackApplied,
            // Provider calculation reference (e.g. Stripe Tax calculation id) for post-completion reporting (WO-37).
            TaxProviderCalculationRef = priced.Tax.ProviderReference,
            AppliedCouponCode = priced.CouponValid ? priced.CouponCode : null,
            ShippingMethodId = priced.SelectedShipping?.MethodId,
            ShippingMethodName = priced.SelectedShipping?.Name,
            ShippingCarrier = priced.SelectedShipping?.Carrier,
            ShippingWasRecommended = priced.SelectedShipping?.IsRecommended ?? false,
        };

        foreach (var line in priced.Lines)
        {
            order.Lines.Add(new OrderLineItem
            {
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                OriginalUnitPrice = line.OriginalUnitPrice,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                CustomAttributesJson = line.CustomAttributesJson,
            });
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = OrderStatus.Pending,
            ToStatus = OrderStatus.Pending,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
            Note = "Order created via checkout.",
        });

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // 8b. Create a payment record and authorize it through the gateway router (applies the gateway's capture mode).
        // The instrument is only meaningful for a gateway that charges more than one on the same credential
        // (Authorize.Net: card vs. ACH/eCheck); it stays null for every other method. It is both stored on the
        // record (so a later capture/refund knows) and passed to the adapter via the request metadata.
        var instrument = ResolvePaymentInstrument(req.PaymentMethod, req.PaymentInstrument);
        var payment = new PaymentRecord
        {
            OrderId = order.Id,
            Method = req.PaymentMethod,
            GatewayName = req.PaymentMethod.ToString(),
            PaymentInstrument = instrument,
            Amount = grandTotal,
            CurrencyCode = order.CurrencyCode,
            Status = PaymentStatus.Pending,
        };
        _db.PaymentRecords.Add(payment);

        var payResult = await _payments.AuthorizeAsync(
            new PaymentRequest(order.Id, req.PaymentMethod, grandTotal, order.CurrencyCode, req.PaymentToken,
                OrderNumber: order.OrderNumber, Metadata: InstrumentMetadata(instrument)),
            ct);

        payment.Status = payResult.Status;
        payment.AuthorizationId = payResult.AuthorizationId;
        payment.TransactionId = payResult.TransactionId;
        payment.GatewayReference = payResult.GatewayReference;
        payment.ErrorMessage = payResult.ErrorMessage;
        if (payResult.Status == PaymentStatus.Captured)
        {
            payment.AuthorizedOnUtc = now;
            payment.CapturedOnUtc = now;
        }
        else if (payResult.Status == PaymentStatus.Authorized)
        {
            payment.AuthorizedOnUtc = now;
        }

        // Mirror the payment state onto the order for at-a-glance status.
        order.PaymentStatus = payResult.Status;
        await _db.SaveChangesAsync(ct);

        // Redirect gateway (Stripe Checkout): the order is created but stays pending while the buyer pays
        // off-site. Remember the source cart so /api/checkout/confirm can consume it once payment succeeds,
        // and hand back the URL for the storefront to redirect to. Nothing is finalized yet.
        if (payResult.RedirectUrl is not null)
        {
            order.SourceCartId = priced.Cart.Id;
            await _db.SaveChangesAsync(ct);
            return OrderResult(order, success: false, error: null, redirectUrl: payResult.RedirectUrl);
        }

        // Client-completed gateway (Razorpay Checkout): the provider order is created, but the buyer must pay
        // in the on-site widget, after which /confirm-client-payment verifies + captures. As with the redirect
        // flow, remember the source cart so it is consumed only on a successful confirmation; nothing is
        // finalized yet, and the order stays Pending/unpaid until the widget's tokens are verified.
        if (payResult.ClientAction is not null)
        {
            order.SourceCartId = priced.Cart.Id;
            await _db.SaveChangesAsync(ct);
            var pending = OrderResult(order, success: false, error: null, redirectUrl: null);
            pending.ClientPayment = BuildClientPayment(
                payResult, order, grandTotal,
                req.ShipTo.FirstName, req.ShipTo.LastName, req.ShipTo.Email, req.ShipTo.PhoneNumber);
            return pending;
        }

        // Payment failed: leave the order Pending/unpaid so the buyer can retry, and do NOT mark the
        // cart checked out or fire any confirmation side effects (AC-PAY-001.3).
        if (!payResult.Success)
        {
            return new CheckoutResult
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status.ToString(),
                Total = grandTotal,
                PaymentStatus = payResult.Status.ToString(),
                Success = false,
                Error = payResult.ErrorMessage
                        ?? "Payment could not be processed. Please try again or choose another payment method.",
            };
        }

        // 8c. Payment succeeded (synchronous gateway): consume the cart and finalize the order.
        priced.Cart.IsCheckedOut = true;
        await _db.SaveChangesAsync(ct);
        await FinalizePlacedOrderAsync(order, ct);

        return OrderResult(order, success: true, error: null, redirectUrl: null, transactionId: payment.TransactionId);
    }

    public async Task<InquiryResult> SubmitInquiryAsync(SubmitInquiryRequest req, CancellationToken ct)
    {
        var commerce = await _commerce.GetAsync(ct);

        // Price it through the shared build path so a submitted inquiry and the quote the buyer saw agree.
        // forPlacement stays false: routing failure is not fatal for a lead, and there is no shipping method
        // to insist on. BuildAsync itself decides inquiry-ness (tenant mode or a quote-only line).
        var priced = await BuildAsync(
            req.CartId, req.SessionId, req.Contact, shippingMethodId: null,
            requestCouponCode: req.CouponCode, forPlacement: false, pickupInStore: false, pickupStoreId: null, ct);

        if (!priced.IsInquiry)
            throw new ConflictException(
                "This cart is a normal order. Please complete checkout and payment instead of submitting an inquiry.");

        var now = _clock.UtcNow;

        // Authenticated buyers get their inquiry linked to their account (so it shows under My Inquiries);
        // guests submit anonymously — a lead form that demanded an account would collect nothing.
        Guid? customerId = null;
        if (_current.UserId is Guid userId)
        {
            customerId = await _db.Customers
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => (Guid?)c.Id)
                .FirstOrDefaultAsync(ct);
        }

        ContactPreference? preferredContact =
            Enum.TryParse<ContactPreference>(req.PreferredContact, ignoreCase: true, out var pref) ? pref : null;

        // The address row is created even when only contact details were collected: Order.ContactName/
        // ContactEmail/ContactPhone are [NotMapped] read-throughs over it, and the notification templates
        // and the admin list all read them from there. Without it the lead would arrive anonymous.
        var order = new Order
        {
            OrderNumber = $"INQ-{now:yyyyMMddHHmmssfff}",
            Status = OrderStatus.Inquiry,
            IsInquiry = true,
            InquiryStatus = Domain.Enums.InquiryStatus.New,
            PaymentStatus = PaymentStatus.NotRequired,
            CustomerId = customerId,
            ShippingAddress = new Address
            {
                FirstName = req.Contact.FirstName,
                LastName = req.Contact.LastName,
                Email = req.Contact.Email,
                PhoneNumber = req.Contact.PhoneNumber,
                Latitude = req.Contact.Latitude,
                Longitude = req.Contact.Longitude,
                CountryCode = req.Contact.CountryCode ?? string.Empty,
                StateProvince = req.Contact.Region,
                PostalCode = req.Contact.PostalCode ?? string.Empty,
                AddressLine1 = req.Contact.Line1 ?? string.Empty,
                AddressLine2 = req.Contact.Line2,
                Landmark = req.Contact.Landmark,
                City = req.Contact.City ?? string.Empty,
            },
            AssignedStoreId = priced.Store?.Id,
            PlacedOnUtc = now,
            RoutedOnUtc = priced.Store is null ? null : now,
            CurrencyCode = priced.Cart.CurrencyCode,
            Subtotal = priced.Subtotal,
            DiscountTotal = priced.DiscountTotal,
            // Deliberately zero: neither is owed until the quote is agreed, and neither provider was called.
            ShippingTotal = 0m,
            TaxTotal = 0m,
            TotalAmount = priced.Total,
            // Recorded for the sales team, but never redeemed — RedeemAsync is only called on a paid order.
            AppliedCouponCode = priced.CouponValid ? priced.CouponCode : null,
            CustomerNote = Trim(req.Message, 4000),
            CompanyName = Trim(req.CompanyName, 200),
            PreferredContact = preferredContact,
            RequiredByUtc = req.RequiredByUtc,
        };

        foreach (var line in priced.Lines)
        {
            order.Lines.Add(new OrderLineItem
            {
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                OriginalUnitPrice = line.OriginalUnitPrice,
                DiscountAmount = line.DiscountAmount,
                LineTotal = line.LineTotal,
                CustomAttributesJson = line.CustomAttributesJson,
            });
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = OrderStatus.Inquiry,
            ToStatus = OrderStatus.Inquiry,
            ChangedById = _current.UserId,
            ChangedOnUtc = now,
            Note = "Inquiry submitted via the storefront.",
        });

        _db.Orders.Add(order);

        // The cart is consumed: the inquiry is now the record of what was asked for. Nothing else about the
        // cart's contents is committed — no stock decrement, no coupon redemption, no points debit.
        priced.Cart.IsCheckedOut = true;
        await _db.SaveChangesAsync(ct);

        await FinalizeInquiryAsync(order, commerce, ct);

        return new InquiryResult
        {
            InquiryId = order.Id,
            ReferenceNumber = order.OrderNumber,
            Status = order.InquiryStatus?.ToString() ?? Domain.Enums.InquiryStatus.New.ToString(),
            EstimatedTotal = order.TotalAmount,
            CurrencyCode = order.CurrencyCode,
            AssignedStoreId = order.AssignedStoreId,
            Success = true,
        };
    }

    public async Task<CheckoutResult> ConfirmAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        // An inquiry has no payment to confirm, verify or retry — refuse rather than let a gateway call
        // be aimed at a record that was never a sale.
        if (order.IsInquiry)
            throw new ConflictException("This record is an inquiry, not a paid order.");

        // Idempotent: already confirmed (e.g. the buyer refreshed the return page or double-confirmed).
        if (order.PaymentStatus == PaymentStatus.Captured)
            return OrderResult(order, success: true, error: null, redirectUrl: null,
                transactionId: await CapturedTransactionIdAsync(orderId, ct));

        var payment = await _db.PaymentRecords
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);
        if (payment is null)
            return OrderResult(order, success: false, error: "No payment to confirm for this order.", redirectUrl: null);

        // Ask the gateway whether the off-site payment actually completed (Stripe session paid?).
        var result = await _payments.VerifyRedirectAsync(payment, ct);
        if (!result.Success || result.Status != PaymentStatus.Captured)
        {
            payment.ErrorMessage = result.ErrorMessage;
            await _db.SaveChangesAsync(ct);
            return OrderResult(order, success: false,
                error: result.ErrorMessage ?? "Payment was not completed. Please try again.", redirectUrl: null);
        }

        return await ApplyCaptureAndFinalizeAsync(order, payment, result, ct);
    }

    public async Task<CheckoutResult> ConfirmClientPaymentAsync(
        Guid orderId, IReadOnlyDictionary<string, string> gatewayData, CancellationToken ct)
    {
        var order = await _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        // An inquiry has no payment to confirm, verify or retry — refuse rather than let a gateway call
        // be aimed at a record that was never a sale.
        if (order.IsInquiry)
            throw new ConflictException("This record is an inquiry, not a paid order.");

        // Idempotent: already captured (e.g. the buyer's widget result was submitted twice).
        if (order.PaymentStatus == PaymentStatus.Captured)
            return OrderResult(order, success: true, error: null, redirectUrl: null,
                transactionId: await CapturedTransactionIdAsync(orderId, ct));

        var payment = await _db.PaymentRecords
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);
        if (payment is null)
            return OrderResult(order, success: false, error: "No payment to confirm for this order.", redirectUrl: null);

        // Verify the on-site widget's tokens (order match + signature + settled amount) and capture.
        var result = await _payments.VerifyClientPaymentAsync(payment, gatewayData, ct);
        if (!result.Success || result.Status != PaymentStatus.Captured)
        {
            payment.ErrorMessage = result.ErrorMessage;
            await _db.SaveChangesAsync(ct);
            return OrderResult(order, success: false,
                error: result.ErrorMessage ?? "Payment was not completed. Please try again.", redirectUrl: null);
        }

        return await ApplyCaptureAndFinalizeAsync(order, payment, result, ct);
    }

    /// <summary>
    /// Marks a verified payment Captured, consumes the source cart (kept intact until now so a cancelled
    /// payment leaves the buyer's cart untouched for a retry), and finalizes the order (stock, coupon,
    /// events, emails). Shared by the redirect (<see cref="ConfirmAsync"/>) and on-site widget
    /// (<see cref="ConfirmClientPaymentAsync"/>) confirmation paths.
    /// </summary>
    private async Task<CheckoutResult> ApplyCaptureAndFinalizeAsync(
        Order order, PaymentRecord payment, PaymentResult result, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        payment.Status = PaymentStatus.Captured;
        payment.TransactionId = result.TransactionId ?? payment.TransactionId;
        payment.AuthorizationId = result.AuthorizationId ?? payment.AuthorizationId;
        payment.GatewayReference = result.GatewayReference ?? payment.GatewayReference;
        payment.AuthorizedOnUtc ??= now;
        payment.CapturedOnUtc = now;
        payment.ErrorMessage = null;
        order.PaymentStatus = PaymentStatus.Captured;

        if (order.SourceCartId is Guid cartId)
        {
            var cart = await _db.Carts.FirstOrDefaultAsync(c => c.Id == cartId && !c.IsCheckedOut, ct);
            if (cart is not null)
                cart.IsCheckedOut = true;
        }
        await _db.SaveChangesAsync(ct);

        await FinalizePlacedOrderAsync(order, ct);

        return OrderResult(order, success: true, error: null, redirectUrl: null, transactionId: payment.TransactionId);
    }

    /// <summary>
    /// Builds the on-site-widget config the storefront opens Razorpay Checkout with, from the gateway's
    /// client action (public key, amount, currency) plus the order's number and the buyer's contact details
    /// (best-effort prefill). The amount is the minor-unit value the gateway created the provider order with.
    /// </summary>
    private static ClientPaymentAction BuildClientPayment(
        PaymentResult payResult, Order order, decimal amount,
        string? firstName, string? lastName, string? email, string? phone)
    {
        var action = payResult.ClientAction!;
        var amountMinor = action.TryGetValue("amount", out var raw)
            && long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
            ? m
            : (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
        var name = $"{firstName} {lastName}".Trim();

        return new ClientPaymentAction
        {
            Provider = action.TryGetValue("provider", out var p) ? p : string.Empty,
            KeyId = action.TryGetValue("keyId", out var k) ? k : string.Empty,
            GatewayOrderId = payResult.GatewayReference ?? string.Empty,
            AmountMinor = amountMinor,
            CurrencyCode = action.TryGetValue("currency", out var c) ? c : order.CurrencyCode,
            OrderNumber = order.OrderNumber,
            CustomerName = string.IsNullOrWhiteSpace(name) ? null : name,
            CustomerEmail = email,
            CustomerPhone = phone,
        };
    }

    public async Task<CheckoutResult> RetryPaymentAsync(Guid orderId, CancellationToken ct)
    {
        // Include the shipping address so an on-site widget retry can prefill the buyer's contact details.
        var order = await _db.Orders
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new NotFoundException(nameof(Order), orderId);

        // An inquiry has no payment to confirm, verify or retry — refuse rather than let a gateway call
        // be aimed at a record that was never a sale.
        if (order.IsInquiry)
            throw new ConflictException("This record is an inquiry, not a paid order.");

        // Already paid — nothing to retry.
        if (order.PaymentStatus == PaymentStatus.Captured)
            return OrderResult(order, success: true, error: null, redirectUrl: null,
                transactionId: await CapturedTransactionIdAsync(orderId, ct));

        var payment = await _db.PaymentRecords
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedOnUtc)
            .FirstOrDefaultAsync(ct);
        var method = payment?.Method ?? PaymentMethodType.Stripe;

        // Re-open a payment session for the same pending order (carry the original instrument forward).
        var payResult = await _payments.AuthorizeAsync(
            new PaymentRequest(order.Id, method, order.TotalAmount, order.CurrencyCode, null,
                OrderNumber: order.OrderNumber, Metadata: InstrumentMetadata(payment?.PaymentInstrument)), ct);

        if (payResult.RedirectUrl is not null)
        {
            if (payment is not null)
            {
                payment.GatewayReference = payResult.GatewayReference;
                payment.Status = PaymentStatus.Pending;
                payment.ErrorMessage = null;
                await _db.SaveChangesAsync(ct);
            }
            return OrderResult(order, success: false, error: null, redirectUrl: payResult.RedirectUrl);
        }

        // On-site widget gateway (Razorpay Checkout): a fresh provider order was created — hand its config
        // back so the storefront can re-open the widget for the same pending order.
        if (payResult.ClientAction is not null)
        {
            if (payment is not null)
            {
                payment.GatewayReference = payResult.GatewayReference;
                payment.Status = PaymentStatus.Pending;
                payment.ErrorMessage = null;
                await _db.SaveChangesAsync(ct);
            }
            var addr = order.ShippingAddress;
            var pending = OrderResult(order, success: false, error: null, redirectUrl: null);
            pending.ClientPayment = BuildClientPayment(
                payResult, order, order.TotalAmount, addr?.FirstName, addr?.LastName, addr?.Email, addr?.PhoneNumber);
            return pending;
        }

        return OrderResult(order, success: false,
            error: payResult.ErrorMessage ?? "Could not start payment. Please try again.", redirectUrl: null);
    }

    /// <summary>
    /// Normalizes the client-supplied instrument into the value stored on the payment record. Only
    /// Authorize.Net distinguishes instruments (card vs. ACH/eCheck): "BankAccount" when the buyer chose
    /// ACH, otherwise "Card". Every other method has a single instrument, so this is null for them.
    /// </summary>
    private static string? ResolvePaymentInstrument(PaymentMethodType method, string? requested)
    {
        if (method != PaymentMethodType.AuthorizeNet)
            return null;
        return PaymentInstruments.IsBankAccount(requested) ? PaymentInstruments.BankAccount : PaymentInstruments.Card;
    }

    /// <summary>Wraps a resolved instrument as payment-request metadata (null when there is no instrument to carry).</summary>
    private static IDictionary<string, string>? InstrumentMetadata(string? instrument)
        => string.IsNullOrEmpty(instrument)
            ? null
            : new Dictionary<string, string> { [PaymentInstruments.MetadataKey] = instrument };

    /// <summary>
    /// The side-effects raised once an order is both placed AND paid: redeem the coupon, publish the
    /// order-placed/routed events, and send the confirmation + store-notification emails. Reconstructed
    /// entirely from the persisted order so it runs identically inline (synchronous gateways) or on the
    /// buyer's return (redirect gateways). The order's <see cref="Order.ShippingAddress"/> and
    /// <see cref="Order.Lines"/> must be loaded.
    /// </summary>
    private async Task FinalizePlacedOrderAsync(Order order, CancellationToken ct)
    {
        // Commit stock for the placed + paid order — the single stock-out path (shared with PlaceOrder).
        // Runs exactly once per order: inline for synchronous gateways, on the buyer's return for redirect
        // gateways (after the already-captured guard), so stock is never decremented twice.
        await _inventory.DecrementForOrderAsync(order, ct);

        if (!string.IsNullOrWhiteSpace(order.AppliedCouponCode))
            await _coupons.RedeemAsync(order.AppliedCouponCode!, ct);

        // Debit the loyalty points redeemed on this order — once, now it is placed + paid (WO-27). Best-effort:
        // a balance change since the buyer staged them must never fail an already-paid order.
        if (order.PointsRedeemed > 0 && order.CustomerId is Guid pointsCustomer)
        {
            try { await _points.RedeemAsync(pointsCustomer, order.PointsRedeemed, order.Id, ct); }
            catch (ConflictException) { /* insufficient balance now; leave the paid order untouched */ }
        }

        await _publisher.Publish(
            new OrderPlaced(order.Id, order.OrderNumber, order.TotalAmount, order.CustomerId, order.ContactEmail),
            ct);

        if (order.AssignedStoreId is Guid routedStoreId)
        {
            var addr = order.ShippingAddress;
            var routing = new RoutingRequest(
                addr?.Latitude, addr?.Longitude, addr?.CountryCode, addr?.StateProvince, addr?.PostalCode,
                order.Lines.Select(l => new RoutingLineItem(l.ProductId, l.ProductVariantId, l.Quantity)).ToList());
            await _publisher.Publish(new OrderRouted(routing, routedStoreId), ct);
        }

        // AC-CHK-003.8: order-confirmation email to the customer, plus a new-order notification to the
        // assigned store — both rendered from admin-editable email templates.
        var shipTo = order.ShippingAddress;
        var contactName = $"{shipTo?.FirstName} {shipTo?.LastName}".Trim();
        var displayName = string.IsNullOrWhiteSpace(contactName) ? "there" : contactName;
        var orderTotalText = $"{order.CurrencyCode} {order.TotalAmount:0.00}";
        var orderDateText = order.PlacedOnUtc.ToString("MMM d, yyyy");
        var toEmail = order.ContactEmail;

        if (!string.IsNullOrWhiteSpace(toEmail))
        {
            await _templates.SendAsync(
                "order.confirmation",
                toEmail,
                string.IsNullOrWhiteSpace(contactName) ? null : contactName,
                new Dictionary<string, string>
                {
                    ["customerName"] = displayName,
                    ["orderNumber"] = order.OrderNumber,
                    ["orderDate"] = orderDateText,
                    ["orderTotal"] = orderTotalText,
                },
                ct);
        }

        if (order.AssignedStoreId is Guid notifyStoreId)
        {
            var store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == notifyStoreId, ct);
            if (store is not null)
            {
                var storeVars = new Dictionary<string, string>
                {
                    ["orderNumber"] = order.OrderNumber,
                    ["storeName"] = store.Name,
                    ["customerName"] = displayName,
                    ["customerEmail"] = toEmail ?? string.Empty,
                    ["orderTotal"] = orderTotalText,
                    ["orderDate"] = orderDateText,
                };
                foreach (var to in ResolveStoreRecipients(store))
                    await _templates.SendAsync("order.store-notification", to, store.Name, storeVars, ct);
            }
        }
    }

    /// <summary>
    /// Side effects of a submitted inquiry (REQ-INQ-001): acknowledge to the buyer, alert the sales team,
    /// raise <see cref="InquirySubmitted"/>. Everything <see cref="FinalizePlacedOrderAsync"/> does for a
    /// sale is deliberately absent — no stock decrement, no coupon redemption, no points debit, and no
    /// <see cref="OrderPlaced"/> (which drives loyalty accrual, marketing and revenue reporting).
    /// </summary>
    private async Task FinalizeInquiryAsync(Order order, CommerceModeSettings commerce, CancellationToken ct)
    {
        await _publisher.Publish(
            new InquirySubmitted(
                order.Id, order.OrderNumber, order.TotalAmount, order.CustomerId,
                order.ContactEmail, order.AssignedStoreId),
            ct);

        var contactName = $"{order.ShippingAddress?.FirstName} {order.ShippingAddress?.LastName}".Trim();
        var displayName = string.IsNullOrWhiteSpace(contactName) ? "there" : contactName;
        var valueText = $"{order.CurrencyCode} {order.TotalAmount:0.00}";
        var dateText = order.PlacedOnUtc.ToString("MMM d, yyyy");
        var itemsSummary = string.Join(", ", order.Lines.Select(l => $"{l.Quantity} × {l.ProductName}"));
        var toEmail = order.ContactEmail;

        if (!string.IsNullOrWhiteSpace(toEmail))
        {
            await _templates.SendAsync(
                "inquiry.acknowledgement",
                toEmail,
                string.IsNullOrWhiteSpace(contactName) ? null : contactName,
                new Dictionary<string, string>
                {
                    ["customerName"] = displayName,
                    ["inquiryNumber"] = order.OrderNumber,
                    ["inquiryDate"] = dateText,
                    ["itemsSummary"] = itemsSummary,
                },
                ct);
        }

        // Recipients: the assigned store's notification addresses plus any tenant-level ones. An inquiry that
        // could not be routed still reaches the tenant list — a lead nobody is told about is a lost lead.
        Domain.Entities.Store? store = null;
        if (order.AssignedStoreId is Guid storeId)
            store = await _db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storeId, ct);

        var recipients = new List<string>();
        if (store is not null)
            recipients.AddRange(ResolveStoreRecipients(store));
        if (!string.IsNullOrWhiteSpace(commerce.NotifyEmails))
        {
            recipients.AddRange(commerce.NotifyEmails
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var storeVars = new Dictionary<string, string>
        {
            ["inquiryNumber"] = order.OrderNumber,
            ["storeName"] = store?.Name ?? string.Empty,
            ["customerName"] = displayName,
            ["customerEmail"] = toEmail ?? string.Empty,
            ["customerPhone"] = order.ContactPhone ?? string.Empty,
            ["companyName"] = order.CompanyName ?? string.Empty,
            ["message"] = order.CustomerNote ?? string.Empty,
            ["itemsSummary"] = itemsSummary,
            ["estimatedValue"] = valueText,
            ["inquiryDate"] = dateText,
        };

        foreach (var to in recipients.Distinct(StringComparer.OrdinalIgnoreCase))
            await _templates.SendAsync("inquiry.store-notification", to, store?.Name, storeVars, ct);
    }

    /// <summary>Trims a buyer-supplied string to the column length, collapsing blanks to null.</summary>
    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    /// <summary>Projects an order into the checkout result shape (shared by place/confirm/retry).</summary>
    private static CheckoutResult OrderResult(
        Order order, bool success, string? error, string? redirectUrl, string? transactionId = null)
        => new()
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Status = order.Status.ToString(),
            Total = order.TotalAmount,
            PaymentStatus = order.PaymentStatus.ToString(),
            Success = success,
            Error = error,
            TransactionId = transactionId,
            RedirectUrl = redirectUrl,
        };

    /// <summary>The gateway transaction id of an order's captured payment (for the confirmation screen), if any.</summary>
    private Task<string?> CapturedTransactionIdAsync(Guid orderId, CancellationToken ct)
        => _db.PaymentRecords
            .AsNoTracking()
            .Where(p => p.OrderId == orderId && p.Status == PaymentStatus.Captured)
            .OrderByDescending(p => p.CapturedOnUtc)
            .Select(p => p.TransactionId)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Recipients for a store's new-order alert: the store's NotificationEmail (one or more addresses
    /// separated by comma/semicolon), falling back to its ContactEmail when the notification field is blank.
    /// </summary>
    private static IEnumerable<string> ResolveStoreRecipients(Domain.Entities.Store store)
    {
        var raw = string.IsNullOrWhiteSpace(store.NotificationEmail) ? store.ContactEmail : store.NotificationEmail;
        if (string.IsNullOrWhiteSpace(raw))
            return Array.Empty<string>();

        return raw
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Runs the shared pricing sequence (steps 1–7) and returns every computed part. For a placement,
    /// the routing-failure and guest-ordering checks are hard stops (throw); for a quote they are
    /// surfaced softly on the returned <see cref="PricedCheckout"/> so the preview can still render.
    /// Multi-address shipping (AC-CHK-003.4) is a future enhancement: a single ship-to address is
    /// priced as one shipment here; splitting a cart across addresses/stores would fan this out per group.
    /// </summary>
    private async Task<PricedCheckout> BuildAsync(
        Guid? cartId, string? sessionId, CheckoutAddress shipTo, string? shippingMethodId,
        string? requestCouponCode, bool forPlacement, bool pickupInStore, Guid? pickupStoreId, CancellationToken ct)
    {
        // 1. Load the cart + its lines and compute the subtotal from the snapshotted unit prices.
        var cart = await LoadCartAsync(cartId, sessionId, ct);
        var items = cart.Items.OrderBy(i => i.CreatedOnUtc).ToList();
        if (items.Count == 0)
            throw new ConflictException("The cart is empty. Add items before checking out.");

        // Apply Customer Group pricing to the lines (member unit prices), keeping the pre-discount base
        // subtotal so the group saving can be itemized in the summary (WO-22) rather than silently folded
        // into the unit prices — where it is otherwise invisible to the buyer.
        var baseLines = await BuildLinesAsync(items, ct);
        var groupId = await _customerGroups.GetCurrentGroupIdAsync(ct);
        var lines = await ApplyGroupPricingAsync(baseLines, groupId, ct);
        var subtotal = lines.Sum(l => l.LineTotal);
        var baseSubtotal = baseLines.Sum(l => l.LineTotal);
        var groupDiscountTotal = baseSubtotal - subtotal;
        // Name the group only when it actually reduced the price (a fixed group price can sit at or above
        // base, in which case there is nothing to itemize).
        string? groupName = null;
        if (groupDiscountTotal > 0m && groupId is Guid gid)
            groupName = await _db.CustomerGroups.AsNoTracking()
                .Where(g => g.Id == gid).Select(g => g.Name).FirstOrDefaultAsync(ct);

        // Inquiry checkout (REQ-INQ-001): either the whole tenant sells by inquiry, or the cart holds a
        // quote-only product. Decided here — after the lines are read — so a quote and its submission
        // always agree, and so no caller can opt out of it from the client.
        var commerce = await _commerce.GetAsync(ct);
        var inquiryLines = lines.Count(l => l.IsInquiryOnly);
        if (!commerce.IsInquiryOnly && inquiryLines > 0 && inquiryLines < lines.Count)
            throw new ConflictException(
                "Quote-only items must be requested on their own. Please remove them from the cart, or check out " +
                "the buyable items separately.");

        if (commerce.IsInquiryOnly || inquiryLines > 0)
            return await BuildInquiryAsync(cart, lines, subtotal, baseSubtotal, groupDiscountTotal, groupName,
                shipTo, commerce, requestCouponCode, ct);

        // Pickup-in-store: skip routing + carrier rates entirely; fulfil at the chosen store (REQ-SHP-004).
        if (pickupInStore)
            return await BuildPickupAsync(cart, lines, subtotal, baseSubtotal, groupDiscountTotal, groupName,
                pickupStoreId, requestCouponCode, forPlacement, ct);

        // 2. Route the order against active stores (ship-to address + line items).
        var routing = await _routing.RouteAsync(BuildRoutingRequest(shipTo, lines), ct);

        if (!routing.IsRouted || routing.AssignedStoreId is not Guid storeId)
        {
            if (forPlacement)
                throw new ConflictException(
                    "This order cannot be fulfilled: no eligible store could be found for the delivery address.");

            // Quote: report the subtotal but flag the cart as not routable; shipping/tax need a store.
            return new PricedCheckout(
                cart, lines, subtotal, routing, Store: null,
                ShippingOptions: Array.Empty<ShippingRateOption>(), SelectedShipping: null, ShippingTotal: 0m,
                Discounts: new List<AppliedDiscount>(), DiscountTotal: 0m,
                Tax: new TaxBreakdown(0m, new(), false), TaxTotal: 0m,
                Total: subtotal, GuestOrderingAllowed: true,
                CouponCode: cart.AppliedCouponCode, CouponValid: false,
                BaseSubtotal: baseSubtotal, GroupDiscountTotal: groupDiscountTotal, GroupDiscountName: groupName);
        }

        // 3. Guest-ordering policy (AC-STR-001.5 / AC-CHK-003.2): load the assigned store and, if it
        // forbids guest ordering, require an authenticated buyer. A placement is rejected with a 401;
        // a quote merely reports GuestOrderingAllowed = false so the UI can prompt login/registration.
        // Include the linked Address: the store's origin country/state/postal/city/lat-long are
        // [NotMapped] read-throughs over Address, and tax (from_country) + carrier rates depend on them.
        var store = await _db.Stores
            .AsNoTracking()
            .Include(s => s.Address)
            .FirstOrDefaultAsync(s => s.Id == storeId, ct)
            ?? throw new ConflictException("The assigned fulfilment store could not be loaded.");

        var isAuthenticated = _current.UserId is not null;
        var guestOrderingAllowed = store.GuestOrderingEnabled || isAuthenticated;
        if (forPlacement && !guestOrderingAllowed)
            throw new UnauthorizedException(
                "Guest checkout is not available for this order. Please log in or create an account to continue.");

        // 4. Shipping: origin = assigned store, destination = ship-to. No per-product weight exists in the
        // catalog, so weight is a best-effort proxy (total units, min 1 kg).
        var weightKg = Math.Max(1m, lines.Sum(l => (decimal)l.Quantity));
        var shippingOptions = await _shipping.GetRatesAsync(
            new CarrierRateRequest(
                new CarrierAddress(store.CountryCode, store.StateProvince, store.PostalCode, store.Latitude, store.Longitude, store.City),
                new CarrierAddress(shipTo.CountryCode, shipTo.Region, shipTo.PostalCode, shipTo.Latitude, shipTo.Longitude, shipTo.City),
                weightKg, Length: null, Width: null, Height: null, OrderSubtotal: subtotal),
            ct);

        // Flag the recommended option (Automatic) or clear the flag (Manual). The recommendation is the
        // default when the buyer has not chosen — it never overrides a choice they did make.
        var selection = await _shippingSelector.SelectAsync(shippingOptions, ct);
        shippingOptions = selection.Options;

        ShippingRateOption? selected;
        if (forPlacement)
        {
            // Nothing quoted. This is never "the order needs no shipping" — no product can be marked
            // non-shippable, and pickup prices its own option on a path that never reaches here. It means
            // every rate source declined or is misconfigured, so refuse rather than ship for free.
            if (!shippingOptions.Any())
                throw new ConflictException(
                    "No delivery options are available for this address. Please try a different address or contact support.");

            if (string.IsNullOrWhiteSpace(shippingMethodId))
            {
                // No method chosen. Under Automatic that is fine — fall back to the recommendation; under
                // Manual nothing is recommended, so the buyer has to pick.
                selected = selection.Recommended;
                if (selected is null)
                    throw new ConflictException("Please choose a shipping method.");
            }
            else
            {
                // The buyer's chosen method must still be on offer.
                selected = shippingOptions.FirstOrDefault(o => o.MethodId == shippingMethodId)
                    ?? throw new ConflictException(
                        $"The selected shipping method '{shippingMethodId}' is no longer available. Please choose another option.");
            }
        }
        else
        {
            // Prefer the requested method if given and still offered; otherwise fall back to the
            // recommendation, or to the cheapest when nothing is recommended (Manual).
            selected = !string.IsNullOrWhiteSpace(shippingMethodId)
                ? shippingOptions.FirstOrDefault(o => o.MethodId == shippingMethodId)
                : null;
            selected ??= selection.Recommended ?? shippingOptions.OrderBy(o => o.Rate).FirstOrDefault();
        }
        var shippingTotal = selected?.Rate ?? 0m;

        // 5. Coupon + discounts. Validate the coupon first so its bound discount is "unlocked" for the
        // engine: coupon-gated rules (RequiresCoupon) apply only when a valid code carrying them is present,
        // while auto-apply rules stack as normal. An invalid coupon is ignored (not applied, not recorded)
        // so checkout stays resilient; cart-applied coupons were already validated at apply time.
        var couponCode = FirstNonEmpty(requestCouponCode, cart.AppliedCouponCode);
        var couponValid = false;
        var unlockedDiscountIds = new List<Guid>();
        if (couponCode is not null)
        {
            var validation = await _coupons.ValidateAsync(couponCode, ct);
            couponValid = validation.IsValid;
            if (validation.IsValid && validation.DiscountId is Guid unlockedId)
                unlockedDiscountIds.Add(unlockedId);
        }

        var discountLines = lines
            .Select(l => new DiscountCartLine(l.ProductId, l.CategoryIds, l.LineTotal, l.Quantity))
            .ToList();
        var discountResult = await _discounts.EvaluateAsync(discountLines, subtotal, unlockedDiscountIds, ct);

        // Tax exemption (AC-TAX-003.3): an authenticated, flagged customer is calculated at zero tax.
        // Resolved here in the shared build path so the quote and the placed order agree. Guests are never exempt.
        TaxExemption? taxExemption = null;
        if (_current.UserId is Guid taxUserId)
        {
            var exempt = await _db.Customers
                .AsNoTracking()
                .Where(c => c.UserId == taxUserId && c.IsTaxExempt)
                .Select(c => new { c.TaxExemptionCertificate, c.VatId })
                .FirstOrDefaultAsync(ct);
            if (exempt is not null)
                taxExemption = new TaxExemption(true, exempt.TaxExemptionCertificate, exempt.VatId);
        }

        // 6. Tax (AC-CHK-003.5): origin = store address, destination = ship-to, plus the taxable shipping cost.
        // Each line is taxed on its discounted value: the discount engine's per-line allocation (order/product/
        // coupon reductions spread across the lines) is netted off before tax, so tax follows the price paid.
        var tax = await _tax.CalculateAsync(
            new TaxCalculationRequest(
                new TaxAddress(store.CountryCode ?? string.Empty, store.StateProvince, store.PostalCode, store.City),
                new TaxAddress(shipTo.CountryCode, shipTo.Region, shipTo.PostalCode, shipTo.City),
                BuildTaxLines(lines, discountResult.LineDiscounts),
                shippingTotal,
                taxExemption),
            ct);

        // 7. Grand total.
        var total = subtotal - discountResult.TotalDiscount + shippingTotal + tax.TotalTax;

        return new PricedCheckout(
            cart, lines, subtotal, routing, store,
            shippingOptions, selected, shippingTotal,
            discountResult.Applied, discountResult.TotalDiscount,
            tax, tax.TotalTax, total, guestOrderingAllowed, couponCode, couponValid,
            BaseSubtotal: baseSubtotal, GroupDiscountTotal: groupDiscountTotal, GroupDiscountName: groupName);
    }

    /// <summary>
    /// Prices an inquiry checkout (REQ-INQ-001): the buyer is requesting a quote, not paying. Lines and
    /// discounts are evaluated exactly as for a sale — so the request carries a realistic value — but no
    /// carrier is rated and no tax provider is called, because neither is owed until the quote is agreed.
    /// A coupon is validated for display only; it is never redeemed here.
    ///
    /// Routing is best-effort: an inquiry with a delivery address is routed like an order so the right
    /// store's team is notified, and one submitted without an address (contact-only mode) falls back to the
    /// configured default store. Unlike a placement, failing to route is not fatal — an unassigned inquiry
    /// still reaches the tenant-level notification recipients rather than being refused at the door.
    /// </summary>
    private async Task<PricedCheckout> BuildInquiryAsync(
        Cart cart, List<LineWork> lines, decimal subtotal,
        decimal baseSubtotal, decimal groupDiscountTotal, string? groupName,
        CheckoutAddress shipTo, CommerceModeSettings commerce, string? requestCouponCode, CancellationToken ct)
    {
        // Route only when there is enough of an address to route on; contact-only inquiries skip straight
        // to the fallback store.
        RoutingResult routing = new(false, null, null, Array.Empty<StoreEvaluation>());
        if (!string.IsNullOrWhiteSpace(shipTo.CountryCode))
            routing = await _routing.RouteAsync(BuildRoutingRequest(shipTo, lines), ct);

        Store? store = null;
        if (routing.AssignedStoreId is Guid routedStoreId)
        {
            store = await _db.Stores.AsNoTracking().Include(s => s.Address)
                .FirstOrDefaultAsync(s => s.Id == routedStoreId, ct);
        }

        // Fallback: the configured default inquiry store, else the only enabled store when there is exactly
        // one (the common single-store tenant). Anything else leaves the inquiry unassigned.
        if (store is null)
        {
            store = commerce.DefaultStoreId is Guid defaultStoreId
                ? await _db.Stores.AsNoTracking().Include(s => s.Address)
                    .FirstOrDefaultAsync(s => s.Id == defaultStoreId && s.IsEnabled, ct)
                : null;

            store ??= await _db.Stores.AsNoTracking().Include(s => s.Address)
                .Where(s => s.IsEnabled)
                .Take(2)
                .ToListAsync(ct) is { Count: 1 } only ? only[0] : null;

            if (store is not null)
            {
                var fallbackAddress = string.Join(", ",
                    new[] { store.AddressLine1, store.City, store.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
                routing = new RoutingResult(true, store.Id, fallbackAddress, Array.Empty<StoreEvaluation>());
            }
        }

        // Coupon + discounts, same engine as a sale — shown on the request so the buyer and the sales team
        // see the same numbers. Redemption is deliberately absent: nothing is consumed by an inquiry.
        var couponCode = FirstNonEmpty(requestCouponCode, cart.AppliedCouponCode);
        var couponValid = false;
        var unlockedDiscountIds = new List<Guid>();
        if (couponCode is not null)
        {
            var validation = await _coupons.ValidateAsync(couponCode, ct);
            couponValid = validation.IsValid;
            if (validation.IsValid && validation.DiscountId is Guid unlockedId)
                unlockedDiscountIds.Add(unlockedId);
        }

        var discountLines = lines
            .Select(l => new DiscountCartLine(l.ProductId, l.CategoryIds, l.LineTotal, l.Quantity))
            .ToList();
        var discountResult = await _discounts.EvaluateAsync(discountLines, subtotal, unlockedDiscountIds, ct);

        var total = subtotal - discountResult.TotalDiscount;

        return new PricedCheckout(
            cart, lines, subtotal, routing, store,
            ShippingOptions: Array.Empty<ShippingRateOption>(), SelectedShipping: null, ShippingTotal: 0m,
            discountResult.Applied, discountResult.TotalDiscount,
            Tax: new TaxBreakdown(0m, new(), false), TaxTotal: 0m,
            Total: total,
            // Guests may always submit an inquiry: a lead form that demands an account collects nothing.
            GuestOrderingAllowed: true,
            CouponCode: couponCode, CouponValid: couponValid,
            BaseSubtotal: baseSubtotal, GroupDiscountTotal: groupDiscountTotal, GroupDiscountName: groupName,
            IsInquiry: true);
    }

    /// <summary>
    /// Prices a pickup-in-store checkout (REQ-SHP-004): no routing/carrier rates — the buyer's chosen store
    /// is the fulfilling store, shipping is free, and tax is collected at the store.
    /// </summary>
    private async Task<PricedCheckout> BuildPickupAsync(
        Cart cart, List<LineWork> lines, decimal subtotal,
        decimal baseSubtotal, decimal groupDiscountTotal, string? groupName, Guid? pickupStoreId,
        string? requestCouponCode, bool forPlacement, CancellationToken ct)
    {
        if (pickupStoreId is not Guid storeId)
            throw new ConflictException("A pickup store is required for pickup-in-store checkout.");

        // Platform switch: collection can be withdrawn everywhere without editing each store. A missing
        // configuration row leaves it available, matching how the rate sources treat an unconfigured install.
        var pickupAllowed = await _db.ShippingProviderConfigurations
            .AsNoTracking()
            .Select(c => (bool?)c.PickupEnabled)
            .FirstOrDefaultAsync(ct) ?? true;
        if (!pickupAllowed)
            throw new ConflictException("Pickup in store is not available right now.");

        var store = await _db.Stores.AsNoTracking().Include(s => s.Address).FirstOrDefaultAsync(s => s.Id == storeId, ct)
            ?? throw new ConflictException("The selected pickup store could not be found.");
        if (!store.IsEnabled || !store.PickupEnabled)
            throw new ConflictException("The selected store does not offer pickup-in-store.");

        var isAuthenticated = _current.UserId is not null;
        var guestOrderingAllowed = store.GuestOrderingEnabled || isAuthenticated;
        if (forPlacement && !guestOrderingAllowed)
            throw new UnauthorizedException(
                "Guest checkout is not available for this order. Please log in or create an account to continue.");

        // A single zero-cost pickup option replaces carrier rates (AC-SHP-004.2).
        var pickupOption = new ShippingRateOption("pickup", "Pickup in store", "Pickup", null, 0m);

        // Coupon + discounts (same engine as delivery): validate first so a coupon-gated discount is
        // unlocked only when its valid code is present.
        var couponCode = FirstNonEmpty(requestCouponCode, cart.AppliedCouponCode);
        var couponValid = false;
        var unlockedDiscountIds = new List<Guid>();
        if (couponCode is not null)
        {
            var validation = await _coupons.ValidateAsync(couponCode, ct);
            couponValid = validation.IsValid;
            if (validation.IsValid && validation.DiscountId is Guid unlockedId)
                unlockedDiscountIds.Add(unlockedId);
        }

        var discountLines = lines
            .Select(l => new DiscountCartLine(l.ProductId, l.CategoryIds, l.LineTotal, l.Quantity))
            .ToList();
        var discountResult = await _discounts.EvaluateAsync(discountLines, subtotal, unlockedDiscountIds, ct);

        // Tax exemption + tax collected at the store (origin = destination = store).
        TaxExemption? taxExemption = null;
        if (_current.UserId is Guid taxUserId)
        {
            var exempt = await _db.Customers
                .AsNoTracking()
                .Where(c => c.UserId == taxUserId && c.IsTaxExempt)
                .Select(c => new { c.TaxExemptionCertificate, c.VatId })
                .FirstOrDefaultAsync(ct);
            if (exempt is not null)
                taxExemption = new TaxExemption(true, exempt.TaxExemptionCertificate, exempt.VatId);
        }

        var storeAddress = new TaxAddress(store.CountryCode ?? string.Empty, store.StateProvince, store.PostalCode, store.City);
        var tax = await _tax.CalculateAsync(
            new TaxCalculationRequest(
                storeAddress, storeAddress,
                BuildTaxLines(lines, discountResult.LineDiscounts),
                0m,
                taxExemption),
            ct);

        var total = subtotal - discountResult.TotalDiscount + tax.TotalTax;
        var addressText = string.Join(", ",
            new[] { store.AddressLine1, store.City, store.PostalCode }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var routing = new RoutingResult(true, storeId, addressText, Array.Empty<StoreEvaluation>());

        return new PricedCheckout(
            cart, lines, subtotal, routing, store,
            new[] { pickupOption }, pickupOption, 0m,
            discountResult.Applied, discountResult.TotalDiscount,
            tax, tax.TotalTax, total, guestOrderingAllowed, couponCode, couponValid,
            BaseSubtotal: baseSubtotal, GroupDiscountTotal: groupDiscountTotal, GroupDiscountName: groupName,
            IsPickup: true);
    }

    /// <summary>
    /// Resolves the cart to check out: by explicit <paramref name="cartId"/>, else the authenticated
    /// customer's active cart, else a guest cart keyed by <paramref name="sessionId"/>. Mirrors the Cart
    /// feature's resolution rules (a customer's cart is restored across sessions). Checked-out carts are
    /// excluded so a completed cart is never re-used.
    /// </summary>
    private async Task<Cart> LoadCartAsync(Guid? cartId, string? sessionId, CancellationToken ct)
    {
        if (cartId is Guid id)
        {
            return await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.Id == id && !c.IsCheckedOut, ct)
                ?? throw new NotFoundException("No active cart exists for the supplied cart id.");
        }

        if (_current.UserId is Guid userId)
        {
            var customer = await _db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

            var customerCart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customer.Id && !c.IsCheckedOut, ct);
            if (customerCart is not null)
                return customerCart;

            // Storefront carts are guest/session-based (built with anonymous cart calls). A logged-in
            // buyer checking out with that session cart resolves it here; the order is still linked to the
            // customer at placement (CustomerId is set from the authenticated user).
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                var authSessionCart = await _db.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.SessionId == sessionId.Trim() && c.CustomerId == null && !c.IsCheckedOut, ct);
                if (authSessionCart is not null)
                    return authSessionCart;
            }

            throw new NotFoundException("No active cart exists for the current user.");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ConflictException(
                "A cart could not be resolved. Provide a cart id or a guest session id, or sign in.");

        var session = sessionId.Trim();
        return await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.SessionId == session && c.CustomerId == null && !c.IsCheckedOut, ct)
            ?? throw new NotFoundException("No active cart exists for the supplied session id.");
    }

    /// <summary>
    /// Projects the cart lines into working rows enriched from the live catalog: product name/SKU (for the
    /// order snapshot), the product's category ids (for category-scoped discounts) and its tax-category
    /// code (for tax). The line's unit price is the cart's snapshot, so pricing is stable across the flow.
    /// </summary>
    private async Task<List<LineWork>> BuildLinesAsync(List<CartItem> items, CancellationToken ct)
    {
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();
        var variantIds = items.Where(i => i.ProductVariantId.HasValue)
            .Select(i => i.ProductVariantId!.Value).Distinct().ToList();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var variants = variantIds.Count == 0
            ? new Dictionary<Guid, ProductVariant>()
            : await _db.ProductVariants
                .AsNoTracking()
                .Where(v => variantIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id, ct);

        var categoriesByProduct = (await _db.ProductCategories
                .AsNoTracking()
                .Where(pc => productIds.Contains(pc.ProductId))
                .Select(pc => new { pc.ProductId, pc.CategoryId })
                .ToListAsync(ct))
            .GroupBy(x => x.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.CategoryId).ToList());

        var taxCategoryIds = products.Values.Select(p => p.TaxCategoryId).Distinct().ToList();
        var taxCategoryCodes = taxCategoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.TaxCategories
                .AsNoTracking()
                .Where(t => taxCategoryIds.Contains(t.Id))
                // No dedicated provider-code column exists; the Tax Category name is the provider tax code.
                .ToDictionaryAsync(t => t.Id, t => t.Name, ct);

        var lines = new List<LineWork>(items.Count);
        foreach (var item in items)
        {
            products.TryGetValue(item.ProductId, out var product);
            ProductVariant? variant = null;
            if (item.ProductVariantId is Guid variantId)
                variants.TryGetValue(variantId, out variant);

            var name = product?.Name ?? "Unavailable product";
            var sku = variant?.Sku ?? product?.Sku;

            string? taxCode = null;
            if (product is not null)
                taxCategoryCodes.TryGetValue(product.TaxCategoryId, out taxCode);

            var categoryIds = categoriesByProduct.TryGetValue(item.ProductId, out var cats)
                ? cats
                : Array.Empty<Guid>();

            lines.Add(new LineWork(
                item.ProductId, item.ProductVariantId, item.Quantity, item.UnitPrice,
                name, sku, taxCode, categoryIds, product?.IsInquiryOnly ?? false,
                OriginalUnitPrice: item.UnitPrice,
                CustomAttributesJson: item.CustomAttributesJson));
        }

        return lines;
    }

    /// <summary>
    /// Applies the authenticated customer's Customer Group pricing to each line (WO-22, AC-CUS-003.5),
    /// running before the subtotal so discounts, tax and the total all derive from the member price. A
    /// no-op for guests and customers with no group (<paramref name="groupId"/> null) — and
    /// <see cref="ICustomerGroupService.ResolvePricesAsync"/> echoes the base price back for non-discounted
    /// lines — so the common checkout path is unchanged.
    /// </summary>
    private async Task<List<LineWork>> ApplyGroupPricingAsync(List<LineWork> lines, Guid? groupId, CancellationToken ct)
    {
        if (lines.Count == 0 || groupId is null)
            return lines;

        // Resolved in one batch: a per-line call would issue two queries per cart line.
        var requests = lines
            .Select(l => new GroupPriceRequest(l.ProductId, l.ProductVariantId, l.UnitPrice))
            .ToList();
        var prices = await _customerGroups.ResolvePricesAsync(requests, groupId, ct);

        var adjusted = new List<LineWork>(lines.Count);
        foreach (var line in lines)
        {
            var price = prices.TryGetValue(new GroupPriceKey(line.ProductId, line.ProductVariantId), out var p)
                ? p
                : line.UnitPrice;
            adjusted.Add(price != line.UnitPrice ? line with { UnitPrice = price } : line);
        }

        return adjusted;
    }

    private static RoutingRequest BuildRoutingRequest(CheckoutAddress shipTo, IReadOnlyList<LineWork> lines) =>
        new(shipTo.Latitude, shipTo.Longitude, shipTo.CountryCode, shipTo.Region, shipTo.PostalCode,
            lines.Select(l => new RoutingLineItem(l.ProductId, l.ProductVariantId, l.Quantity)).ToList());

    /// <summary>
    /// Projects the priced lines into tax inputs, attaching each line's share of the discount total (the
    /// discount engine's per-line allocation, index-aligned to <paramref name="lines"/>) so the provider
    /// taxes the discounted line value. A line with no allocated discount carries zero.
    /// </summary>
    private static List<TaxLineInput> BuildTaxLines(IReadOnlyList<LineWork> lines, IReadOnlyList<decimal> lineDiscounts) =>
        lines.Select((l, i) => new TaxLineInput(
            l.ProductId, l.TaxCategoryCode, l.UnitPrice, l.Quantity,
            i < lineDiscounts.Count ? lineDiscounts[i] : 0m)).ToList();

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        return null;
    }

    /// <summary>A cart line enriched with the catalog data the checkout sequence needs.</summary>
    private sealed record LineWork(
        Guid ProductId,
        Guid? ProductVariantId,
        int Quantity,
        decimal UnitPrice,
        string ProductName,
        string? Sku,
        string? TaxCategoryCode,
        IReadOnlyList<Guid> CategoryIds,
        bool IsInquiryOnly,
        decimal OriginalUnitPrice,
        // The buyer's typed CustomInput values, carried verbatim from the cart line onto the order line.
        string? CustomAttributesJson = null)
    {
        public decimal LineTotal => UnitPrice * Quantity;

        /// <summary>The Customer Group saving on this line: (list − charged) × qty, never negative.</summary>
        public decimal DiscountAmount => Math.Max(0m, (OriginalUnitPrice - UnitPrice) * Quantity);
    }

    /// <summary>The full set of computed parts shared between a quote and its placement.</summary>
    private sealed record PricedCheckout(
        Cart Cart,
        List<LineWork> Lines,
        decimal Subtotal,
        RoutingResult Routing,
        Store? Store,
        IReadOnlyList<ShippingRateOption> ShippingOptions,
        ShippingRateOption? SelectedShipping,
        decimal ShippingTotal,
        List<AppliedDiscount> Discounts,
        decimal DiscountTotal,
        TaxBreakdown Tax,
        decimal TaxTotal,
        decimal Total,
        bool GuestOrderingAllowed,
        string? CouponCode,
        bool CouponValid,
        decimal BaseSubtotal = 0m,
        decimal GroupDiscountTotal = 0m,
        string? GroupDiscountName = null,
        bool IsInquiry = false,
        bool IsPickup = false);
}
