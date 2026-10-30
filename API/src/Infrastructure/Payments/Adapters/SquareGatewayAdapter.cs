using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Payments.Adapters;

/// <summary>
/// Square gateway adapter (WO-33) over the Payments REST API via <see cref="IHttpClientFactory"/>. The
/// access token is stored in Credentials_Square; the credential's <c>IsProduction</c> flag selects the
/// live vs. sandbox endpoint. The card nonce/source id is tokenized on the storefront by the Web Payments
/// SDK and supplied as the request's <see cref="PaymentRequest.PaymentToken"/>. <see cref="CaptureMode"/>
/// maps to the CreatePayment <c>autocomplete</c> flag.
/// </summary>
public class SquareGatewayAdapter : PaymentGatewayAdapterBase
{
    private const string SquareVersion = "2024-01-18";

    public SquareGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<SquareGatewayAdapter> logger)
        : base(vault, httpClientFactory, logger) { }

    public override PaymentMethodType Method => PaymentMethodType.Square;

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var ctx = await ResolveContextAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("Square access token is not configured (Credentials_Square).");
            var (baseUrl, token, locationId) = ctx.Value;
            if (string.IsNullOrWhiteSpace(req.PaymentToken))
                return PaymentResult.Failed("Square requires a card nonce / source id (PaymentToken).");

            // The card nonce/source id is tokenized on the storefront by the Square Web Payments SDK and sent
            // as the request's PaymentToken; CreatePayment charges it here. location_id is stamped when the
            // credential names a Location — Square falls back to the account's main location otherwise, so it
            // is omitted rather than sent null.
            var minor = ToMinorUnits(req.Amount);
            var payload = new Dictionary<string, object?>
            {
                ["source_id"] = req.PaymentToken,
                ["idempotency_key"] = Guid.NewGuid().ToString("N"),
                ["amount_money"] = new { amount = minor, currency = req.CurrencyCode },
                ["autocomplete"] = mode == CaptureMode.AuthorizeAndCapture,
            };
            if (!string.IsNullOrWhiteSpace(locationId))
                payload["location_id"] = locationId;

            using var resp = await SendAsync(HttpMethod.Post, token, $"{baseUrl}/payments", JsonSerializer.Serialize(payload), ct);

            // Diagnostic (no secrets): exactly what we asked Square to charge and what it returned, so a wrong
            // Base URL, an auth failure, a currency/location mismatch or an empty body are visible in the logs.
            Logger.LogInformation(
                "Square create-payment: url={Url} currency={Currency} amountMinor={AmountMinor} locationId={LocationId} → HTTP {Status} body={Body}",
                $"{baseUrl}/payments", req.CurrencyCode, minor, locationId ?? "(none)", (int)resp.Status, Snippet(resp.Raw));

            if (!resp.Root.TryGetProperty("payment", out var payment))
                return PaymentResult.Failed($"Square could not process the payment: {DescribeError(resp)}");

            var id = GetString(payment, "id");
            var status = GetString(payment, "status");
            return status switch
            {
                "COMPLETED" => PaymentResult.Captured(id, req.Amount, id, id),
                "APPROVED" => PaymentResult.Authorized(id, id),
                _ => PaymentResult.Failed($"Square payment status '{status}'."),
            };
        });

    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var ctx = await ResolveContextAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("Square access token is not configured (Credentials_Square).");
            var (baseUrl, token, _) = ctx.Value;

            var paymentId = payment.AuthorizationId ?? payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(paymentId))
                return PaymentResult.Failed("Square capture requires the original payment id.");

            using var resp = await SendAsync(HttpMethod.Post, token, $"{baseUrl}/payments/{paymentId}/complete", "{}", ct);
            if (!resp.Root.TryGetProperty("payment", out var completed))
                return PaymentResult.Failed($"Square capture failed: {DescribeError(resp)}");

            return string.Equals(GetString(completed, "status"), "COMPLETED", StringComparison.OrdinalIgnoreCase)
                ? PaymentResult.Captured(paymentId, amount, paymentId, paymentId)
                : PaymentResult.Failed($"Square capture returned status '{GetString(completed, "status")}'.");
        });

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var ctx = await ResolveContextAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("Square access token is not configured (Credentials_Square).");
            var (baseUrl, token, _) = ctx.Value;

            var paymentId = payment.TransactionId ?? payment.AuthorizationId ?? payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(paymentId))
                return PaymentResult.Failed("Square refund requires the original payment id.");

            var body = JsonSerializer.Serialize(new
            {
                idempotency_key = Guid.NewGuid().ToString("N"),
                payment_id = paymentId,
                amount_money = new { amount = ToMinorUnits(amount), currency = payment.CurrencyCode },
            });

            using var resp = await SendAsync(HttpMethod.Post, token, $"{baseUrl}/refunds", body, ct);
            if (!resp.Root.TryGetProperty("refund", out var refund))
                return PaymentResult.Failed($"Square refund failed: {DescribeError(resp)}");

            var status = GetString(refund, "status");
            return status is "PENDING" or "COMPLETED"
                ? new PaymentResult(true, PaymentStatus.Refunded, TransactionId: GetString(refund, "id"), GatewayReference: GetString(refund, "id"), CapturedAmount: amount)
                : PaymentResult.Failed($"Square refund returned status '{status}'.");
        });

    /// <summary>
    /// Resolves the active Square context: the base URL (its admin-set live/sandbox host), the access token,
    /// and the optional Location id. The vault packs the token + location into a small JSON envelope; a legacy
    /// bare-token value is still accepted. Null when unconfigured (no access token).
    /// </summary>
    private async Task<(string BaseUrl, string Token, string? LocationId)?> ResolveContextAsync(CancellationToken ct)
    {
        var resolved = await ResolveAsync(ct);
        if (resolved is null)
            return null;
        var baseUrl = RequireBaseUrl(resolved);

        string token;
        string? locationId = null;
        var raw = resolved.Value;
        if (raw.TrimStart().StartsWith('{'))
        {
            using var doc = JsonDocument.Parse(raw);
            token = GetString(doc.RootElement, "accessToken") ?? string.Empty;
            locationId = GetString(doc.RootElement, "locationId");
        }
        else
        {
            token = raw; // Legacy: the value was the bare access token before the JSON envelope.
        }

        return string.IsNullOrWhiteSpace(token) ? null : (baseUrl, token, locationId);
    }

    private async Task<GatewayResponse> SendAsync(HttpMethod method, string accessToken, string url, string? json, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("Square-Version", SquareVersion);

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
    /// A human-readable reason from a Square response: its <c>errors[0].detail</c>/<c>code</c> when present,
    /// otherwise the HTTP status plus a snippet of the raw body — so an auth failure, a wrong Base URL (which
    /// often returns an empty or non-JSON body) and a currency/location mismatch are all distinguishable
    /// rather than collapsing to an opaque "unexpected response".
    /// </summary>
    private static string DescribeError(GatewayResponse resp)
    {
        if (resp.Root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
        {
            var detail = GetString(errors[0], "detail") ?? GetString(errors[0], "code");
            if (!string.IsNullOrWhiteSpace(detail))
                return detail!;
        }

        var snippet = resp.Raw.Trim();
        if (snippet.Length > 300)
            snippet = snippet[..300] + "…";
        return string.IsNullOrWhiteSpace(snippet)
            ? $"HTTP {(int)resp.Status} {resp.Status} with an empty response — check the Base URL ends with /v2 and matches the token's environment (sandbox vs. live)."
            : $"HTTP {(int)resp.Status} {resp.Status}: {snippet}";
    }

    /// <summary>A trimmed, length-capped copy of a raw gateway body for diagnostic logging.</summary>
    private static string Snippet(string? raw)
    {
        var s = (raw ?? string.Empty).Trim();
        return s.Length > 600 ? s[..600] + "…" : s;
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
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

        public void Dispose() => _doc.Dispose();
    }
}
