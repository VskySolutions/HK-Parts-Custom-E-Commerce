using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
// Stripe.net also declares a PaymentRecord type — alias ours so the adapter signatures resolve unambiguously.
using PaymentRecord = HKPartsEcommerce.Domain.Entities.PaymentRecord;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Stripe gateway adapter (WO-32) using the official <c>Stripe.net</c> SDK with the redirect-based
/// <b>Checkout Session</b> flow. Authorization creates a hosted Checkout Session and returns its URL
/// (<see cref="PaymentResult.Redirect"/>); Stripe collects the card on its own page, so no card token is
/// needed here. On the buyer's return, <see cref="VerifyRedirectAsync"/> confirms the session was paid.
/// Credentials (secret key + return URL) come from the active Credentials_Stripe row.
/// </summary>
public class StripeGatewayAdapter : PaymentGatewayAdapterBase
{
    public StripeGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<StripeGatewayAdapter> logger)
        : base(vault, httpClientFactory, logger) { }

    public override PaymentMethodType Method => PaymentMethodType.Stripe;

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var cred = await ResolveStripeAsync(ct);
            if (cred is null || string.IsNullOrWhiteSpace(cred.Value.SecretKey))
                return PaymentResult.Failed("Stripe is not configured — add an Active Stripe credential.");
            if (string.IsNullOrWhiteSpace(cred.Value.ReturnUrl))
                return PaymentResult.Failed("Stripe return URL is not configured on the Stripe credential.");

            var secretKey = cred.Value.SecretKey;
            var returnUrl = cred.Value.ReturnUrl!.Trim();
            // Stripe validates these as URLs, so the return URL must be an absolute http(s) address.
            if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri)
                || (returnUri.Scheme != Uri.UriSchemeHttp && returnUri.Scheme != Uri.UriSchemeHttps))
                return PaymentResult.Failed($"Stripe return URL must be an absolute http(s) URL — got '{returnUrl}'.");

            // Buyer returns to {returnUrl}?order=… on success, or …&cancelled=1 on cancel. We look the
            // Checkout Session up server-side by its stored id on confirm, so no {CHECKOUT_SESSION_ID}
            // placeholder is needed (its literal braces are invalid URL characters and Stripe rejects them).
            var sep = returnUrl.Contains('?') ? '&' : '?';
            var successUrl = $"{returnUrl}{sep}order={req.OrderId}";
            var cancelUrl = $"{returnUrl}{sep}order={req.OrderId}&cancelled=1";

            // Human-readable label for the Stripe page + dashboard; keep the raw id in metadata for lookup.
            var orderLabel = string.IsNullOrWhiteSpace(req.OrderNumber) ? $"Order {req.OrderId:N}" : $"Order {req.OrderNumber}";

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                ClientReferenceId = string.IsNullOrWhiteSpace(req.OrderNumber) ? req.OrderId.ToString() : req.OrderNumber,
                Metadata = new Dictionary<string, string> { ["orderId"] = req.OrderId.ToString() },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Quantity = 1,
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = req.CurrencyCode.ToLowerInvariant(),
                            UnitAmount = ToMinorUnits(req.Amount),
                            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = orderLabel },
                        },
                    },
                },
            };

            try
            {
                var session = await new SessionService(new StripeClient(secretKey)).CreateAsync(options, cancellationToken: ct);
                return PaymentResult.Redirect(session.Url, session.Id);
            }
            catch (Exception ex)
            {
                // Surface the exact URLs + Stripe's own reason so a rejected redirect URL is diagnosable.
                var detail = (ex as StripeException)?.StripeError?.Message ?? ex.Message;
                Logger.LogWarning(ex, "Stripe Checkout Session create failed. success_url={SuccessUrl} cancel_url={CancelUrl}", successUrl, cancelUrl);
                return PaymentResult.Failed($"Stripe rejected the checkout session: {detail} (success_url='{successUrl}').");
            }
        });

    // Checkout Sessions in payment mode capture on completion, so there is nothing to capture separately.
    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => Task.FromResult(PaymentResult.Captured(payment.TransactionId ?? payment.GatewayReference, amount, payment.AuthorizationId, payment.GatewayReference));

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var cred = await ResolveStripeAsync(ct);
            if (cred is null || string.IsNullOrWhiteSpace(cred.Value.SecretKey))
                return PaymentResult.Failed("Stripe is not configured.");

            var paymentIntentId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(paymentIntentId))
                return PaymentResult.Failed("Stripe refund requires the settled PaymentIntent id.");

            var refund = await new RefundService(new StripeClient(cred.Value.SecretKey)).CreateAsync(
                new RefundCreateOptions { PaymentIntent = paymentIntentId, Amount = ToMinorUnits(amount) },
                cancellationToken: ct);

            return refund.Status is "succeeded" or "pending"
                ? new PaymentResult(true, PaymentStatus.Refunded, TransactionId: refund.Id, GatewayReference: refund.Id, CapturedAmount: amount)
                : PaymentResult.Failed($"Stripe refund returned status '{refund.Status}'.");
        });

    public override Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct)
        => GuardAsync("verify", async () =>
        {
            var cred = await ResolveStripeAsync(ct);
            if (cred is null || string.IsNullOrWhiteSpace(cred.Value.SecretKey))
                return PaymentResult.Failed("Stripe is not configured.");

            var sessionId = payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(sessionId))
                return PaymentResult.Failed("No Stripe checkout session to verify.", PaymentStatus.Pending);

            var session = await new SessionService(new StripeClient(cred.Value.SecretKey)).GetAsync(sessionId, cancellationToken: ct);

            return string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                ? PaymentResult.Captured(session.PaymentIntentId ?? sessionId, payment.Amount, session.PaymentIntentId, sessionId)
                : PaymentResult.Failed($"Payment not completed (status '{session.PaymentStatus}').", PaymentStatus.Pending);
        });

    /// <summary>Resolves the active Stripe credential ({ secretKey, returnUrl } JSON) from the vault.</summary>
    private async Task<(string? SecretKey, string? ReturnUrl)?> ResolveStripeAsync(CancellationToken ct)
    {
        var resolved = await ResolveAsync(ct);
        if (resolved is null)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(resolved.Value);
            var root = doc.RootElement;
            return (GetString(root, "secretKey"), GetString(root, "returnUrl"));
        }
        catch (JsonException)
        {
            // Legacy: a bare secret-key string with no return URL.
            return (resolved.Value, null);
        }
    }

    private static string? GetString(JsonElement root, string name)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
}
