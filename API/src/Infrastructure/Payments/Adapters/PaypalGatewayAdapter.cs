using System.Globalization;
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
/// PayPal gateway adapter (WO-32) using the Orders v2 REST API over <see cref="IHttpClientFactory"/>.
/// Credentials are stored as <c>clientId:secret</c> (Credentials_PayPal); the credential's
/// <c>IsProduction</c> flag selects the live vs. sandbox endpoint. The adapter first exchanges the
/// credential for an OAuth2 access token, then creates/authorizes/captures orders. <see cref="CaptureMode"/>
/// maps to the order <c>intent</c> (AUTHORIZE vs. CAPTURE).
/// </summary>
public class PaypalGatewayAdapter : PaymentGatewayAdapterBase
{

    public PaypalGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<PaypalGatewayAdapter> logger)
        : base(vault, httpClientFactory, logger) { }

    public override PaymentMethodType Method => PaymentMethodType.PayPal;

    public override Task<PaymentResult> AuthorizeAsync(PaymentRequest req, CaptureMode mode, CancellationToken ct)
        => GuardAsync("authorize", async () =>
        {
            var ctx = await AuthenticateAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("PayPal credentials are not configured (format 'clientId:secret').");
            var (baseUrl, token, returnUrl) = ctx.Value;

            // Server-side completion of an order the buyer already approved on the client.
            if (!string.IsNullOrWhiteSpace(req.PaymentToken))
            {
                var action = mode == CaptureMode.AuthorizeOnly ? "authorize" : "capture";
                using var actDoc = await PostJsonAsync(token, $"{baseUrl}/v2/checkout/orders/{req.PaymentToken}/{action}", "{}", ct);
                var actRoot = actDoc.RootElement;
                var actStatus = GetString(actRoot, "status");

                if (!string.Equals(actStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                    return PaymentResult.Failed($"PayPal order {action} returned status '{actStatus}'.");

                return mode == CaptureMode.AuthorizeOnly
                    ? PaymentResult.Authorized(ExtractPaymentId(actRoot, "authorizations") ?? req.PaymentToken, req.PaymentToken)
                    : PaymentResult.Captured(ExtractPaymentId(actRoot, "captures"), req.Amount, req.PaymentToken, req.PaymentToken);
            }

            // No approval token: create the order for the REDIRECT flow. The buyer must approve on PayPal's
            // hosted page (the returned "approve" link) before any money moves, so hand that URL back as a
            // redirect — the order is captured on the buyer's return in VerifyRedirectAsync. Returning a plain
            // "pending success" here (the previous bug) made checkout finalize the order as paid=Pending
            // without ever sending the buyer to PayPal.
            if (string.IsNullOrWhiteSpace(returnUrl))
                return PaymentResult.Failed("PayPal return URL is not configured — set it on the PayPal integration.");
            if (!Uri.TryCreate(returnUrl.Trim(), UriKind.Absolute, out var returnUri)
                || (returnUri.Scheme != Uri.UriSchemeHttp && returnUri.Scheme != Uri.UriSchemeHttps))
                return PaymentResult.Failed($"PayPal return URL must be an absolute http(s) URL — got '{returnUrl}'.");

            var intent = mode == CaptureMode.AuthorizeOnly ? "AUTHORIZE" : "CAPTURE";
            var body = BuildCreateOrderJson(intent, req, returnUrl.Trim());
            using var doc = await PostJsonAsync(token, $"{baseUrl}/v2/checkout/orders", body, ct);
            var root = doc.RootElement;

            var orderId = GetString(root, "id");
            if (string.IsNullOrWhiteSpace(orderId))
                return PaymentResult.Failed($"PayPal: {DescribeError(root)}");

            var approveUrl = ExtractApproveLink(root);
            if (string.IsNullOrWhiteSpace(approveUrl))
                return PaymentResult.Failed("PayPal did not return an approval link for the order.");

            // GatewayReference = the PayPal order id, so VerifyRedirectAsync can capture it on return.
            return PaymentResult.Redirect(approveUrl, orderId);
        });

    public override Task<PaymentResult> CaptureAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("capture", async () =>
        {
            var ctx = await AuthenticateAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("PayPal credentials are not configured.");
            var (baseUrl, token, _) = ctx.Value;

            var orderId = payment.AuthorizationId ?? payment.GatewayReference;
            if (string.IsNullOrWhiteSpace(orderId))
                return PaymentResult.Failed("PayPal capture requires the original order id.");

            using var doc = await PostJsonAsync(token, $"{baseUrl}/v2/checkout/orders/{orderId}/capture", "{}", ct);
            var root = doc.RootElement;
            var status = GetString(root, "status");

            return string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                ? PaymentResult.Captured(ExtractPaymentId(root, "captures"), amount, orderId, orderId)
                : PaymentResult.Failed($"PayPal capture returned status '{status}'.");
        });

    public override Task<PaymentResult> RefundAsync(PaymentRecord payment, decimal amount, CancellationToken ct)
        => GuardAsync("refund", async () =>
        {
            var ctx = await AuthenticateAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("PayPal credentials are not configured.");
            var (baseUrl, token, _) = ctx.Value;

            var captureId = payment.TransactionId;
            if (string.IsNullOrWhiteSpace(captureId))
                return PaymentResult.Failed("PayPal refund requires the capture id (TransactionId).");

            var body = JsonSerializer.Serialize(new
            {
                amount = new { value = amount.ToString("0.00", CultureInfo.InvariantCulture), currency_code = payment.CurrencyCode },
            });
            using var doc = await PostJsonAsync(token, $"{baseUrl}/v2/payments/captures/{captureId}/refund", body, ct);
            var root = doc.RootElement;
            var status = GetString(root, "status");

            return status is "COMPLETED" or "PENDING"
                ? new PaymentResult(true, PaymentStatus.Refunded, TransactionId: GetString(root, "id"), GatewayReference: GetString(root, "id"), CapturedAmount: amount)
                : PaymentResult.Failed($"PayPal refund returned status '{status}'.");
        });

    /// <summary>
    /// Called when the buyer returns from PayPal's approval page (redirect flow). Reads the order and, once
    /// the buyer has approved it, captures it — mirroring how Stripe's session is verified on return. The
    /// order id was stored as the payment's <see cref="PaymentRecord.GatewayReference"/> at authorize time.
    /// </summary>
    public override Task<PaymentResult> VerifyRedirectAsync(PaymentRecord payment, CancellationToken ct)
        => GuardAsync("verify", async () =>
        {
            var ctx = await AuthenticateAsync(ct);
            if (ctx is null)
                return PaymentResult.Failed("PayPal credentials are not configured.", PaymentStatus.Pending);
            var (baseUrl, token, _) = ctx.Value;

            var orderId = payment.GatewayReference ?? payment.AuthorizationId;
            if (string.IsNullOrWhiteSpace(orderId))
                return PaymentResult.Failed("No PayPal order to verify.", PaymentStatus.Pending);

            // Read the order to see whether the buyer approved it yet.
            using var getDoc = await GetJsonAsync(token, $"{baseUrl}/v2/checkout/orders/{orderId}", ct);
            var getRoot = getDoc.RootElement;
            var status = GetString(getRoot, "status");

            // Idempotent: already captured (buyer refreshed the return page / double-confirm).
            if (string.Equals(status, "COMPLETED", StringComparison.OrdinalIgnoreCase))
                return PaymentResult.Captured(ExtractPaymentId(getRoot, "captures") ?? orderId, payment.Amount, orderId, orderId);

            if (!string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase))
                return PaymentResult.Failed($"PayPal payment not completed (status '{status}').", PaymentStatus.Pending);

            using var capDoc = await PostJsonAsync(token, $"{baseUrl}/v2/checkout/orders/{orderId}/capture", "{}", ct);
            var capRoot = capDoc.RootElement;
            var capStatus = GetString(capRoot, "status");
            return string.Equals(capStatus, "COMPLETED", StringComparison.OrdinalIgnoreCase)
                ? PaymentResult.Captured(ExtractPaymentId(capRoot, "captures"), payment.Amount, orderId, orderId)
                : PaymentResult.Failed($"PayPal capture returned status '{capStatus}'.", PaymentStatus.Pending);
        });

    private static string BuildCreateOrderJson(string intent, PaymentRequest req, string returnUrl)
    {
        // Buyer returns to {returnUrl}?order={orderId} on approval, or …&cancelled=1 on cancel — the same
        // storefront checkout return-handling as Stripe, so our order id round-trips for confirmation.
        var sep = returnUrl.Contains('?') ? '&' : '?';
        var returnTo = $"{returnUrl}{sep}order={req.OrderId}";
        var cancelTo = $"{returnUrl}{sep}order={req.OrderId}&cancelled=1";

        var order = new Dictionary<string, object?>
        {
            ["intent"] = intent,
            ["purchase_units"] = new[]
            {
                new { amount = new { currency_code = req.CurrencyCode, value = req.Amount.ToString("0.00", CultureInfo.InvariantCulture) } },
            },
            ["application_context"] = new { return_url = returnTo, cancel_url = cancelTo },
        };
        return JsonSerializer.Serialize(order);
    }

    /// <summary>
    /// Resolves the active PayPal credential, picks the live/sandbox base URL from its production flag, and
    /// exchanges the client id/secret for an OAuth2 access token. Returns null when unconfigured or auth fails.
    /// </summary>
    private async Task<(string BaseUrl, string Token, string? ReturnUrl)?> AuthenticateAsync(CancellationToken ct)
    {
        var resolved = await ResolveAsync(ct);
        if (resolved is null)
            return null;

        var parts = resolved.Value.Split(':', 2);
        if (parts.Length != 2)
            return null;

        var baseUrl = RequireBaseUrl(resolved);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{parts[0]}:{parts[1]}"));

        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
        var token = GetString(doc.RootElement, "access_token");
        return token is null ? null : (baseUrl, token, resolved.ReturnUrl);
    }

    private async Task<JsonDocument> PostJsonAsync(string accessToken, string url, string json, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
    }

    private async Task<JsonDocument> GetJsonAsync(string accessToken, string url, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, ct);
        var payload = await response.Content.ReadAsStringAsync(ct);
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
    }

    /// <summary>The buyer-facing approval URL from the created order's <c>links[]</c> (rel "approve"/"payer-action").</summary>
    private static string? ExtractApproveLink(JsonElement root)
    {
        if (!root.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var link in links.EnumerateArray())
        {
            var rel = GetString(link, "rel");
            if (string.Equals(rel, "approve", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rel, "payer-action", StringComparison.OrdinalIgnoreCase))
                return GetString(link, "href");
        }
        return null;
    }

    /// <summary>Digs the first capture/authorization id out of purchase_units[].payments.{collection}[].</summary>
    private static string? ExtractPaymentId(JsonElement root, string collection)
    {
        if (!root.TryGetProperty("purchase_units", out var units) || units.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var unit in units.EnumerateArray())
        {
            if (unit.TryGetProperty("payments", out var payments)
                && payments.TryGetProperty(collection, out var items)
                && items.ValueKind == JsonValueKind.Array
                && items.GetArrayLength() > 0)
            {
                return GetString(items[0], "id");
            }
        }
        return null;
    }

    private static string DescribeError(JsonElement root)
        => GetString(root, "message") ?? GetString(root, "name") ?? "order was not created";

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
