using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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
public class SquareGatewayAdapter : PaymentGatewayAdapterBase, ISquareClient
{
    private const string SquareVersion = "2024-01-18";

    /// <summary>An OAuth access token is renewed once it is this close to its expiry (Square tokens last 30 days).</summary>
    public static readonly TimeSpan RefreshWindow = TimeSpan.FromDays(7);

    private readonly IApplicationDbContext? _db;
    private readonly IDateTimeProvider? _clock;

    public SquareGatewayAdapter(ICredentialVault vault, IHttpClientFactory httpClientFactory, ILogger<SquareGatewayAdapter> logger,
        IApplicationDbContext? db = null, IDateTimeProvider? clock = null)
        : base(vault, httpClientFactory, logger)
    {
        _db = db;
        _clock = clock;
    }

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

            // PHP parity "Transaction Type": the row's setting wins; without one, the gateway's capture-mode setting.
            var rowType = _db is null ? null : await _db.SquareCredentials.AsNoTracking().Where(c => c.Active)
                .OrderByDescending(c => c.UpdatedOnUtc).Select(c => c.TransactionType).FirstOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(rowType))
                mode = string.Equals(rowType, "AuthOnly", StringComparison.OrdinalIgnoreCase) ? CaptureMode.AuthorizeOnly : CaptureMode.AuthorizeAndCapture;

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

            // Authorize only: a held (APPROVED) payment can't be refunded, only cancelled, which releases the funds.
            if (payment.Status == PaymentStatus.Authorized)
            {
                using var cancel = await SendAsync(HttpMethod.Post, token, $"{baseUrl}/payments/{paymentId}/cancel", "{}", ct);
                if (!cancel.Root.TryGetProperty("payment", out var cancelled))
                    return PaymentResult.Failed($"Square void failed: {DescribeError(cancel)}");
                return string.Equals(GetString(cancelled, "status"), "CANCELED", StringComparison.OrdinalIgnoreCase)
                    ? new PaymentResult(true, PaymentStatus.Voided, TransactionId: paymentId, GatewayReference: paymentId)
                    : PaymentResult.Failed($"Square void returned status '{GetString(cancelled, "status")}'.");
            }

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

        if (string.IsNullOrWhiteSpace(token))
            return null;

        // PHP parity "Refresh Token" / "Token Expires At": an OAuth access token is renewed before it lapses.
        token = await RefreshIfDueAsync(baseUrl, token, ct) ?? token;
        return (baseUrl, token, locationId);
    }

    /// <summary>
    /// Renews the active row's OAuth access token when it expires within <see cref="RefreshWindow"/> and a refresh
    /// token + Application ID + Secret are stored: Square's ObtainToken (grant_type = refresh_token). The new token,
    /// expiry and (if Square rotated it) refresh token are saved on the row. A failed refresh keeps the current token
    /// while it still works; once it has expired the error is surfaced so the admin can sign in to Square again.
    /// Returns the token to use, or null to keep the one already resolved.
    /// </summary>
    private async Task<string?> RefreshIfDueAsync(string baseUrl, string currentToken, CancellationToken ct)
    {
        if (_db is null)
            return null;
        var row = await _db.SquareCredentials.Where(c => c.Active).OrderByDescending(c => c.UpdatedOnUtc).FirstOrDefaultAsync(ct);
        var now = _clock?.UtcNow ?? DateTime.UtcNow;
        if (row is null || row.AccessTokenExpiresAtUtc is not DateTime expires || expires - now > RefreshWindow
            || string.IsNullOrWhiteSpace(row.RefreshToken) || string.IsNullOrWhiteSpace(row.ApplicationId)
            || string.IsNullOrWhiteSpace(row.ApplicationSecret))
            return null;

        var oauthUrl = $"{SquareOAuthBase(baseUrl)}/oauth2/token";
        var body = JsonSerializer.Serialize(new
        {
            client_id = row.ApplicationId,
            client_secret = row.ApplicationSecret,
            grant_type = "refresh_token",
            refresh_token = row.RefreshToken,
        });
        using var resp = await SendAsync(HttpMethod.Post, string.Empty, oauthUrl, body, ct);
        var newToken = GetString(resp.Root, "access_token");
        if (string.IsNullOrWhiteSpace(newToken))
        {
            Logger.LogWarning("Square OAuth token refresh failed: HTTP {Status} {Error}", (int)resp.Status, DescribeError(resp));
            if (expires <= now)
                throw new InvalidOperationException("The Square access token has expired and could not be renewed. Reconnect Square or enter a new access token and refresh token.");
            return null;
        }

        row.AccessToken = newToken;
        if (DateTime.TryParse(GetString(resp.Root, "expires_at"), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var newExpiry))
            row.AccessTokenExpiresAtUtc = newExpiry;
        if (GetString(resp.Root, "refresh_token") is { Length: > 0 } rotated)
            row.RefreshToken = rotated;
        await _db.SaveChangesAsync(ct);
        Logger.LogInformation("Square OAuth access token renewed; it now expires {Expiry:u}.", row.AccessTokenExpiresAtUtc);
        return newToken;
    }

    /// <summary>
    /// PHP parity "View Locations": signs in with the given row's access token (renewing it first when it is the
    /// active row and due) and lists the account's locations, so the admin can check the token and copy a Location ID.
    /// </summary>
    public async Task<GatewayTestResult> ListLocationsAsync(Guid credentialId, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var row = _db is null ? null : await _db.SquareCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId, ct);
        if (row is null || string.IsNullOrWhiteSpace(row.AccessToken))
            return new GatewayTestResult { Connected = false, Message = "This Square credential has no access token." };

        var baseUrl = (row.BaseUrl ?? (row.IsProduction ? "https://connect.squareup.com/v2" : "https://connect.squareupsandbox.com/v2")).Trim().TrimEnd('/');
        var token = row.AccessToken;
        try
        {
            if (row.Active)
                token = await RefreshIfDueAsync(baseUrl, token, ct) ?? token;

            using var resp = await SendAsync(HttpMethod.Get, token, $"{baseUrl}/locations", null, ct);
            var result = new GatewayTestResult
            {
                Environment = row.IsProduction ? "Production" : "Sandbox",
                ElapsedMs = started.ElapsedMilliseconds,
            };
            if (!resp.Root.TryGetProperty("locations", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                result.Connected = false;
                result.Message = DescribeError(resp);
                return result;
            }

            result.Connected = true;
            result.Locations = list.EnumerateArray().Select(l =>
            {
                string? address = null;
                if (l.TryGetProperty("address", out var a) && a.ValueKind == JsonValueKind.Object)
                    address = string.Join(", ", new[] { GetString(a, "address_line_1"), GetString(a, "locality"),
                        GetString(a, "administrative_district_level_1"), GetString(a, "postal_code") }.Where(s => !string.IsNullOrWhiteSpace(s)));
                return new GatewayLocation
                {
                    Id = GetString(l, "id") ?? string.Empty,
                    Name = GetString(l, "name"),
                    Status = GetString(l, "status"),
                    Currency = GetString(l, "currency"),
                    Address = string.IsNullOrWhiteSpace(address) ? null : address,
                };
            }).ToList();
            result.Message = row.LocationId is { Length: > 0 } chosen && result.Locations.All(x => x.Id != chosen)
                ? $"The saved Location ID '{chosen}' is not one of this account's locations."
                : null;
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            return new GatewayTestResult { Connected = false, Message = ex.Message, ElapsedMs = started.ElapsedMilliseconds };
        }
    }

    /// <summary>The OAuth host for a Payments base URL: <c>https://connect.squareup.com/v2</c> → <c>https://connect.squareup.com</c>.</summary>
    public static string SquareOAuthBase(string baseUrl)
    {
        var trimmed = baseUrl.Trim().TrimEnd('/');
        return trimmed.EndsWith("/v2", StringComparison.OrdinalIgnoreCase) ? trimmed[..^3] : trimmed;
    }

    private async Task<GatewayResponse> SendAsync(HttpMethod method, string accessToken, string url, string? json, CancellationToken ct)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(method, url);
        if (json is not null)
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(accessToken)) // ObtainToken (OAuth refresh) is called without one
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
