using System.Net.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.IntegrationCredentials;
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
/// <para>
/// PHP parity, the Stripe method settings on that row: <b>Transaction Type</b> (AuthOnly = the PaymentIntent is
/// created with manual capture, the funds are held and captured later through <see cref="CaptureAsync"/>),
/// <b>Customer Profile Enabled</b> (a Stripe Customer is created for a signed-in buyer and the card saved to it; its
/// id is kept on the payment) and <b>Accepted Card Types</b> (Stripe Checkout cannot restrict brands up front, so a
/// card of another brand is voided or refunded on return and the buyer asked for a different card).
/// </para>
/// </summary>
public class StripeGatewayAdapter : PaymentGatewayAdapterBase
{
    private readonly IApplicationDbContext? _db;

    public StripeGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<StripeGatewayAdapter> logger,
        IApplicationDbContext? db = null)
        : base(vault, httpClientFactory, logger)
        => _db = db;

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

            var settings = await MethodSettingsAsync(ct);
            // Transaction Type on the row wins; without one, the gateway's capture-mode setting decides.
            var authOnly = settings?.TransactionType is { Length: > 0 } t
                ? string.Equals(t, "AuthOnly", StringComparison.OrdinalIgnoreCase)
                : mode == CaptureMode.AuthorizeOnly;

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                ClientReferenceId = string.IsNullOrWhiteSpace(req.OrderNumber) ? req.OrderId.ToString() : req.OrderNumber,
                Metadata = new Dictionary<string, string> { ["orderId"] = req.OrderId.ToString() },
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    CaptureMethod = authOnly ? "manual" : "automatic",
                    Metadata = new Dictionary<string, string> { ["orderId"] = req.OrderId.ToString() },
                },
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

            // Customer Profile Enabled: signed-in buyers get a Stripe Customer with the card saved for later use.
            var email = req.Customer?.Email;
            if (settings?.CustomerProfilesEnabled == true && req.Customer?.CustomerId is not null && !string.IsNullOrWhiteSpace(email))
            {
                options.CustomerCreation = "always";
                options.CustomerEmail = email.Trim();
                options.PaymentIntentData.SetupFutureUsage = "off_session";
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                options.CustomerEmail = email.Trim(); // receipt + prefill only
            }

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

    /// <summary>
    /// Automatic-capture sessions are already captured on completion. An authorize-only payment (manual capture)
    /// is captured now through the PaymentIntent, for the amount asked (Stripe allows less than authorized).
    /// </summary>
    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var paymentIntentId = payment.AuthorizationId ?? payment.TransactionId;
            if (payment.Status != PaymentStatus.Authorized || string.IsNullOrWhiteSpace(paymentIntentId) || !paymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
                return PaymentResult.Captured(payment.TransactionId ?? payment.GatewayReference, amount, payment.AuthorizationId, payment.GatewayReference);

            var cred = await ResolveStripeAsync(ct);
            if (cred is null || string.IsNullOrWhiteSpace(cred.Value.SecretKey))
                return PaymentResult.Failed("Stripe is not configured.");

            var service = new PaymentIntentService(new StripeClient(cred.Value.SecretKey));
            var intent = await service.CaptureAsync(paymentIntentId,
                new PaymentIntentCaptureOptions { AmountToCapture = ToMinorUnits(amount) }, cancellationToken: ct);
            return intent.Status == "succeeded"
                ? PaymentResult.Captured(intent.Id, amount, intent.Id, payment.GatewayReference)
                : PaymentResult.Failed($"Stripe capture returned status '{intent.Status}'.", PaymentStatus.Authorized);
        });

    /// <summary>A captured payment is refunded; a payment still only authorized is voided (Stripe cannot refund it).</summary>
    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var cred = await ResolveStripeAsync(ct);
            if (cred is null || string.IsNullOrWhiteSpace(cred.Value.SecretKey))
                return PaymentResult.Failed("Stripe is not configured.");

            var paymentIntentId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(paymentIntentId))
                return PaymentResult.Failed("Stripe refund requires the settled PaymentIntent id.");

            var client = new StripeClient(cred.Value.SecretKey);
            if (payment.Status == PaymentStatus.Authorized && paymentIntentId.StartsWith("pi_", StringComparison.Ordinal))
            {
                var cancelled = await new PaymentIntentService(client).CancelAsync(paymentIntentId, cancellationToken: ct);
                return cancelled.Status == "canceled"
                    ? new PaymentResult(true, PaymentStatus.Voided, TransactionId: cancelled.Id, GatewayReference: cancelled.Id)
                    : PaymentResult.Failed($"Stripe void returned status '{cancelled.Status}'.");
            }

            var refund = await new RefundService(client).CreateAsync(
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

            var client = new StripeClient(cred.Value.SecretKey);
            var session = await new SessionService(client).GetAsync(sessionId,
                new SessionGetOptions { Expand = new List<string> { "payment_intent.latest_charge" } }, cancellationToken: ct);
            var intent = session.PaymentIntent;
            var piId = session.PaymentIntentId ?? intent?.Id;

            var paid = string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase);
            var held = intent?.Status == "requires_capture"; // authorize-only: the card is approved, funds held
            if (!paid && !held)
                return PaymentResult.Failed($"Payment not completed (status '{session.PaymentStatus}').", PaymentStatus.Pending);

            var card = intent?.LatestCharge?.PaymentMethodDetails?.Card;
            var details = new PaymentGatewayDetails
            {
                CardType = card?.Brand,
                CardLastFour = card?.Last4,
                CustomerProfileId = session.CustomerId,
            };

            // Accepted Card Types: refuse a brand the store doesn't take, releasing the money straight away.
            var settings = await MethodSettingsAsync(ct);
            var accepted = StripeCardTypes.Split(settings?.AcceptedCardTypes)
                .Select(t => StripeCardTypes.StripeBrandByType.TryGetValue(t, out var b) ? b : t.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (accepted.Count > 0 && card?.Brand is { Length: > 0 } brand && !accepted.Contains(brand) && piId is not null)
            {
                if (held)
                    await new PaymentIntentService(client).CancelAsync(piId, cancellationToken: ct);
                else
                    await new RefundService(client).CreateAsync(new RefundCreateOptions { PaymentIntent = piId }, cancellationToken: ct);
                return PaymentResult.Failed(
                    $"Sorry, we don't accept {BrandLabel(brand)} cards. Your card was not charged. Please pay with a different card.",
                    PaymentStatus.Pending) with { Details = details };
            }

            var result = held
                ? PaymentResult.Authorized(piId, sessionId)
                : PaymentResult.Captured(piId ?? sessionId, payment.Amount, piId, sessionId);
            return result with { Details = details };
        });

    private static string BrandLabel(string stripeBrand) =>
        StripeCardTypes.StripeBrandByType.FirstOrDefault(kv => string.Equals(kv.Value, stripeBrand, StringComparison.OrdinalIgnoreCase)).Key
        ?? stripeBrand;

    /// <summary>The active Stripe row's method settings (transaction type, customer profiles, accepted cards).</summary>
    private async Task<StripeMethodSettings?> MethodSettingsAsync(CancellationToken ct)
    {
        if (_db is null)
            return null;
        return await _db.StripeCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .Select(c => new StripeMethodSettings(c.TransactionType, c.CustomerProfilesEnabled, c.AcceptedCardTypes))
            .FirstOrDefaultAsync(ct);
    }

    private sealed record StripeMethodSettings(string? TransactionType, bool CustomerProfilesEnabled, string? AcceptedCardTypes);

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
