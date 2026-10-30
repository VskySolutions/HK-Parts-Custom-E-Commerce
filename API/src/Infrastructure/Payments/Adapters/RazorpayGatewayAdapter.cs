using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Razorpay gateway adapter (WO-33) over the REST API via <see cref="IHttpClientFactory"/>, using the
/// on-site <b>Razorpay Checkout</b> widget flow. Credentials live in the Credentials_Razorpay row as
/// <c>key_id:key_secret</c> (sent as HTTP Basic auth). <see cref="AuthorizeAsync"/> creates a Razorpay
/// order and hands the storefront the public key + order id to open the widget; the buyer pays inside it,
/// and <see cref="VerifyClientPaymentAsync"/> verifies the returned signature server-side and captures.
/// Amounts are billed in the currency's minor unit (e.g. cents/paise). <see cref="CaptureMode"/> maps to the order's
/// <c>payment_capture</c> flag (auto-capture vs. capture-on-verify).
/// </summary>
public class RazorpayGatewayAdapter : PaymentGatewayAdapterBase
{
    // Razorpay currently transacts in INR only (the account has no international-payment capability), so every
    // order is billed in the INR equivalent of the order's base-currency amount, converted at the exchange
    // rate held in the currency master. Hard-coded to INR for now; revisit if Razorpay is enabled for USD.
    private const string RazorpayCurrency = "INR";

    private readonly ICurrencyService _currency;

    public RazorpayGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ICurrencyService currency, ILogger<RazorpayGatewayAdapter> logger)
        : base(vault, httpClientFactory, logger) => _currency = currency;

    public override PaymentMethodType Method => PaymentMethodType.Razorpay;

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var auth = await GetAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Razorpay is not configured — set Key ID, Key Secret and Base URL on the Razorpay integration.");
            var (basic, keyId, _, baseUrl) = auth.Value;

            // Razorpay is charged in INR: convert the order's amount to its INR equivalent (rate from the
            // currency master) and bill that. The same currency + amount are handed to the widget below —
            // Razorpay Checkout must open against the currency and amount its provider order was created with.
            var currency = RazorpayCurrency;

            // Create the order the on-site Checkout widget charges against. payment_capture selects whether
            // Razorpay auto-captures once the buyer pays (AuthorizeAndCapture) or leaves it authorized for the
            // verify step to capture.
            var minor = await InrMinorAsync(req.Amount, ct);
            var orderBody = JsonSerializer.Serialize(new
            {
                amount = minor,
                currency,
                receipt = req.OrderNumber,
                payment_capture = mode == CaptureMode.AuthorizeAndCapture,
            });
            using var resp = await SendAsync(HttpMethod.Post, basic, $"{baseUrl}/orders", orderBody, ct);

            var orderId = GetString(resp.Root, "id");

            // Diagnostic (no secrets): exactly what we asked Razorpay to create and what it returned, so a
            // gateway-side rejection — e.g. an account that does not accept the currency — is visible in the logs.
            Logger.LogInformation(
                "Razorpay create-order: sentCurrency={Currency} sentAmountMinor={AmountMinor} → HTTP {Status} orderId={OrderId} body={Body}",
                currency, minor, (int)resp.Status, orderId ?? "(none)", Snippet(resp.Raw));

            if (string.IsNullOrWhiteSpace(orderId))
                return PaymentResult.Failed($"Razorpay could not create the payment order: {DescribeError(resp)}");

            // GatewayReference = the Razorpay order id, so VerifyClientPaymentAsync can tie the widget's
            // payment back to the order we created. The client action carries the public key + amount the
            // storefront opens the widget with.
            return PaymentResult.ClientActionRequired(orderId, new Dictionary<string, string>
            {
                ["provider"] = "razorpay",
                ["keyId"] = keyId,
                ["amount"] = minor.ToString(CultureInfo.InvariantCulture),
                ["currency"] = currency,
            });
        });

    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var auth = await GetAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Razorpay is not configured.");
            var (basic, _, _, baseUrl) = auth.Value;

            // Capture acts on the Razorpay PAYMENT id (recorded as TransactionId once the widget payment is
            // verified), never the order id.
            var paymentId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(paymentId))
                return PaymentResult.Failed("Razorpay capture requires the payment id.");

            // Capture the INR equivalent — the payment settled in INR.
            var body = JsonSerializer.Serialize(new { amount = await InrMinorAsync(amount, ct), currency = RazorpayCurrency });
            using var resp = await SendAsync(HttpMethod.Post, basic, $"{baseUrl}/payments/{paymentId}/capture", body, ct);
            return MapCapture(resp, amount, paymentId!);
        });

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var auth = await GetAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Razorpay is not configured.");
            var (basic, _, _, baseUrl) = auth.Value;

            var paymentId = payment.TransactionId ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(paymentId))
                return PaymentResult.Failed("Razorpay refund requires the payment id.");

            // Refund the INR equivalent (the payment settled in INR); the body carries no currency — Razorpay
            // refunds in the payment's own currency.
            var body = JsonSerializer.Serialize(new { amount = await InrMinorAsync(amount, ct) });
            using var resp = await SendAsync(HttpMethod.Post, basic, $"{baseUrl}/payments/{paymentId}/refund", body, ct);
            var refundId = GetString(resp.Root, "id");

            return refundId is not null
                ? new PaymentResult(true, PaymentStatus.Refunded, TransactionId: refundId, GatewayReference: refundId, CapturedAmount: amount)
                : PaymentResult.Failed($"Razorpay refund failed: {DescribeError(resp)}");
        });

    /// <summary>
    /// Verifies the tokens the on-site Razorpay Checkout widget returned (<c>razorpay_payment_id</c>,
    /// <c>razorpay_order_id</c>, <c>razorpay_signature</c>): the order must be the one we created, the HMAC
    /// signature must check out, and the settled amount must match. Captures if the payment is only
    /// authorized, then reports it Captured.
    /// </summary>
    public override Task<PaymentResult> VerifyClientPaymentAsync(PaymentRecord payment, IReadOnlyDictionary<string, string> data, CancellationToken ct)
        => GuardAsync("verify", async () =>
        {
            var auth = await GetAuthAsync(ct);
            if (auth is null)
                return PaymentResult.Failed("Razorpay is not configured.", PaymentStatus.Pending);
            var (basic, _, keySecret, baseUrl) = auth.Value;

            var paymentId = Value(data, "razorpay_payment_id");
            var orderId = Value(data, "razorpay_order_id");
            var signature = Value(data, "razorpay_signature");
            if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(signature))
                return PaymentResult.Failed("Razorpay payment response was incomplete.", PaymentStatus.Pending);

            // The order id the widget reports must be the very order we created for this payment record — so a
            // client cannot substitute a different (cheaper) order.
            if (!string.IsNullOrWhiteSpace(payment.GatewayReference)
                && !string.Equals(orderId, payment.GatewayReference, StringComparison.Ordinal))
                return PaymentResult.Failed("Razorpay order mismatch — the payment does not belong to this order.", PaymentStatus.Pending);

            // Authenticate the callback: HMAC-SHA256("{order_id}|{payment_id}", key_secret) must equal the
            // signature Razorpay signed the successful payment with.
            if (!SignatureValid(orderId!, paymentId!, keySecret, signature!))
                return PaymentResult.Failed("Razorpay signature verification failed.", PaymentStatus.Pending);

            // Read the payment back to confirm it settled for the expected amount — never trust the client for money.
            using var get = await SendAsync(HttpMethod.Get, basic, $"{baseUrl}/payments/{paymentId}", null, ct);
            if (!get.IsSuccess)
                return PaymentResult.Failed($"Razorpay payment lookup failed: {DescribeError(get)}", PaymentStatus.Pending);

            var status = GetString(get.Root, "status");
            var paidMinor = GetInt64(get.Root, "amount");
            // The payment settled in INR, so compare against the INR equivalent of the order amount.
            var expectedMinor = await InrMinorAsync(payment.Amount, ct);
            if (paidMinor is not null && paidMinor.Value != expectedMinor)
                return PaymentResult.Failed(
                    $"Razorpay amount mismatch — expected {expectedMinor}, received {paidMinor}.", PaymentStatus.Pending);

            // "captured" → settled (auto-capture). "authorized" → capture now so the on-site order is paid.
            if (string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase))
                return PaymentResult.Captured(paymentId, payment.Amount, paymentId, orderId);

            if (string.Equals(status, "authorized", StringComparison.OrdinalIgnoreCase))
            {
                var body = JsonSerializer.Serialize(new { amount = expectedMinor, currency = RazorpayCurrency });
                using var cap = await SendAsync(HttpMethod.Post, basic, $"{baseUrl}/payments/{paymentId}/capture", body, ct);
                return MapCapture(cap, payment.Amount, paymentId!, orderId);
            }

            return PaymentResult.Failed($"Razorpay payment not completed (status '{status ?? "unknown"}').", PaymentStatus.Pending);
        });

    private static PaymentResult MapCapture(GatewayResponse resp, decimal amount, string paymentId, string? orderId = null)
    {
        var status = GetString(resp.Root, "status");
        return string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase)
            ? PaymentResult.Captured(paymentId, amount, paymentId, orderId ?? paymentId)
            : PaymentResult.Failed($"Razorpay capture returned status '{status ?? DescribeError(resp)}'.");
    }

    private static bool SignatureValid(string orderId, string paymentId, string keySecret, string signature)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{orderId}|{paymentId}"));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        // FixedTimeEquals returns false (does not throw) when the two byte arrays differ in length.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant()));
    }

    /// <summary>Basic-auth header value + key id/secret + endpoint for the active credential; null when unconfigured.</summary>
    private async Task<(string Basic, string KeyId, string KeySecret, string BaseUrl)?> GetAuthAsync(CancellationToken ct)
    {
        var resolved = await ResolveAsync(ct);
        if (resolved is null)
            return null;

        var parts = resolved.Value.Split(':', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return null;

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(resolved.Value));
        return (basic, parts[0], parts[1], RequireBaseUrl(resolved));
    }

    /// <summary>
    /// The INR minor-unit (paise) amount for a base-currency <paramref name="baseAmount"/>: convert to INR at
    /// the exchange rate held in the currency master (<see cref="ICurrencyService"/>), then to the smallest
    /// unit. Razorpay is billed in INR, so this feeds every amount sent to it — order creation, capture, refund
    /// and the <see cref="VerifyClientPaymentAsync"/> amount cross-check — keeping them all in agreement. Throws
    /// (surfaced as a failed result via <see cref="PaymentGatewayAdapterBase.GuardAsync"/>) when no INR currency
    /// is configured in the currency master.
    /// </summary>
    private async Task<long> InrMinorAsync(decimal baseAmount, CancellationToken ct)
        => ToMinorUnits(await _currency.ConvertAsync(baseAmount, RazorpayCurrency, ct));

    private async Task<GatewayResponse> SendAsync(HttpMethod method, string basic, string url, string? json, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await client.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
        }
        catch (JsonException)
        {
            // Non-JSON body (e.g. an HTML error page from a wrong Base URL). Keep the raw text for diagnostics.
            doc = JsonDocument.Parse("{}");
        }
        return new GatewayResponse(response.StatusCode, doc, raw);
    }

    /// <summary>
    /// A human-readable reason from a gateway response: Razorpay's <c>error.description</c> when present,
    /// otherwise the HTTP status plus a snippet of the raw body — so a wrong Base URL, an auth failure or an
    /// empty response are all distinguishable rather than collapsing to "unexpected response".
    /// </summary>
    private static string DescribeError(GatewayResponse resp)
    {
        if (resp.Root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
        {
            var description = GetString(error, "description");
            if (!string.IsNullOrWhiteSpace(description))
                return description!;
        }

        var snippet = resp.Raw.Trim();
        if (snippet.Length > 300)
            snippet = snippet[..300] + "…";
        return string.IsNullOrWhiteSpace(snippet)
            ? $"HTTP {(int)resp.Status} {resp.Status} with an empty response."
            : $"HTTP {(int)resp.Status} {resp.Status}: {snippet}";
    }

    /// <summary>A trimmed, length-capped copy of a raw gateway body for diagnostic logging.</summary>
    private static string Snippet(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        return s.Length > 600 ? s[..600] + "…" : s;
    }

    private static string? Value(IReadOnlyDictionary<string, string> data, string key)
        => data.TryGetValue(key, out var v) ? v : null;

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static long? GetInt64(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n)
            ? n
            : null;

    /// <summary>An HTTP response captured for both parsing and diagnostics (status + parsed JSON + raw body).</summary>
    private sealed class GatewayResponse : IDisposable
    {
        private readonly JsonDocument _doc;

        public GatewayResponse(HttpStatusCode status, JsonDocument doc, string raw)
        {
            Status = status;
            _doc = doc;
            Raw = raw;
        }

        public HttpStatusCode Status { get; }
        public string Raw { get; }
        public JsonElement Root => _doc.RootElement;
        public bool IsSuccess => (int)Status is >= 200 and < 300;

        public void Dispose() => _doc.Dispose();
    }
}
