using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Shipping;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.AddressVerification;

/// <summary>
/// USPS Addresses API v3 (<c>GET /addresses/v3/address</c>) over OAuth 2.0 client credentials (AV-20), replacing
/// the retired Web Tools <c>Verify</c> call. Uses the active USPS credential from the shipping credentials
/// screen (AD-62). All input travels as encoded query values (AV-22). US addresses only.
/// Paths and field names follow the USPS v3 catalog as understood at build time; confirm against the current
/// USPS developer portal before go-live (§9).
/// </summary>
public class UspsAddressProvider : IAddressVerificationProvider
{
    private const string TokenPath = "/oauth2/v3/token";
    private const string AddressPath = "/addresses/v3/address";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, (string Token, DateTimeOffset ExpiresAt)> TokenCache = new();

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<UspsAddressProvider> _logger;

    public UspsAddressProvider(IHttpClientFactory httpClientFactory, IApplicationDbContext db, ILogger<UspsAddressProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public AddressVerificationProvider Provider => AddressVerificationProvider.Usps;

    public Task<bool> IsConfiguredAsync(CancellationToken ct) =>
        _db.UspsCredentials.AsNoTracking().AnyAsync(c => c.Active
            && c.BaseUrl != null && c.ConsumerKey != null && c.ConsumerSecret != null, ct);

    public async Task<AddressVerificationOutcome> VerifyAsync(VerifiableAddress address, bool includeRaw, CancellationToken ct)
    {
        var credential = await _db.UspsCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .Select(c => new { c.BaseUrl, c.ConsumerKey, c.ConsumerSecret })
            .FirstOrDefaultAsync(ct);
        if (credential is null || string.IsNullOrWhiteSpace(credential.BaseUrl)
            || string.IsNullOrWhiteSpace(credential.ConsumerKey) || string.IsNullOrWhiteSpace(credential.ConsumerSecret))
            return Unavailable("USPS is not configured (no active USPS credential).");

        var baseUrl = credential.BaseUrl!.TrimEnd('/');
        var client = _httpClientFactory.CreateClient("usps-addresses");
        client.Timeout = HttpTimeout;

        var token = await GetTokenAsync(client, baseUrl, credential.ConsumerKey!, credential.ConsumerSecret!, ct);
        if (token is null)
            return Unavailable("USPS refused the consumer key / secret.");

        var (zip5, zip4) = SplitZip(address.PostalCode);
        var query = new Dictionary<string, string?>
        {
            ["streetAddress"] = address.Line1?.Trim(),
            ["secondaryAddress"] = string.IsNullOrWhiteSpace(address.Line2) ? null : address.Line2.Trim(),
            ["city"] = address.City?.Trim(),
            ["state"] = ShippingRulesEngine.NormalizeUsState(address.Region),
            ["ZIPCode"] = zip5,
            ["ZIPPlus4"] = zip4,
        };
        var url = baseUrl + AddressPath + "?" + string.Join("&", query
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        HttpResponseMessage response;
        try
        {
            response = await SendWithRetryAsync(client, () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Get, url);
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return message;
            }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("USPS address verification could not be reached: {Error}", ex.Message);
            return Unavailable("USPS could not be reached.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                TokenCache.TryRemove(TokenKey(baseUrl, credential.ConsumerKey!, credential.ConsumerSecret!), out _);

            if (!response.IsSuccessStatusCode)
            {
                // 400/404 with an error body = USPS could not match the address; anything else = USPS trouble.
                if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                {
                    var reasons = ReadErrors(body);
                    if (reasons.Count == 0) reasons.Add("Address not found.");
                    return new AddressVerificationOutcome
                    {
                        Status = AddressVerificationStatus.Invalid,
                        Messages = reasons,
                        RawResponse = includeRaw ? body : null,
                    };
                }

                _logger.LogWarning("USPS address verification: HTTP {StatusCode}.", (int)response.StatusCode);
                var unavailable = Unavailable($"USPS answered HTTP {(int)response.StatusCode}.");
                unavailable.RawResponse = includeRaw ? body : null;
                return unavailable;
            }

            var outcome = Interpret(address, body);
            outcome.RawResponse = includeRaw ? body : null;
            return outcome;
        }
    }

    /// <summary>Reads a 200 answer: DPV confirmation, the standardised address and whether it differs from the input.</summary>
    public static AddressVerificationOutcome Interpret(VerifiableAddress input, string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var outcome = new AddressVerificationOutcome();

        if (!root.TryGetProperty("address", out var a) || a.ValueKind != JsonValueKind.Object)
        {
            outcome.Status = AddressVerificationStatus.Invalid;
            outcome.Messages.Add("Address not found.");
            return outcome;
        }

        var zip5 = Str(a, "ZIPCode");
        var zip4 = Str(a, "ZIPPlus4");
        var suggestion = new VerifiableAddress(
            Str(a, "streetAddress"),
            Str(a, "secondaryAddress"),
            Str(a, "city"),
            Str(a, "state"),
            string.IsNullOrWhiteSpace(zip4) ? zip5 : $"{zip5}-{zip4}",
            "US");
        outcome.Suggestion = suggestion;

        string? dpv = null;
        if (root.TryGetProperty("additionalInfo", out var info) && info.ValueKind == JsonValueKind.Object)
        {
            dpv = Str(info, "DPVConfirmation");
            var business = Str(info, "business");
            if (business is "Y") outcome.IsResidential = false;
            else if (business is "N") outcome.IsResidential = true;
        }

        foreach (var list in new[] { "corrections", "warnings" })
            if (root.TryGetProperty(list, out var items) && items.ValueKind == JsonValueKind.Array)
                foreach (var item in items.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.Object && Str(item, "text") is { Length: > 0 } text)
                        outcome.Messages.Add(text);
                    else if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } s)
                        outcome.Messages.Add(s);

        // DPV: Y = deliverable; D = apartment/suite missing; S = apartment/suite not recognised; N = not deliverable.
        if (dpv is "N")
        {
            outcome.Status = AddressVerificationStatus.Invalid;
            if (outcome.Messages.Count == 0) outcome.Messages.Add("Address not found.");
            return outcome;
        }
        if (dpv is "D")
            outcome.Messages.Add("The apartment, suite or unit number is missing.");
        else if (dpv is "S")
            outcome.Messages.Add("The apartment, suite or unit number was not recognised.");

        outcome.Status = dpv is "D" or "S" || Differs(input, suggestion)
            ? AddressVerificationStatus.Suggested
            : AddressVerificationStatus.Valid;
        return outcome;
    }

    /// <summary>
    /// True when the standardised address changes what the buyer typed beyond letter case and punctuation.
    /// Adding ZIP+4 alone is not a change worth asking about.
    /// </summary>
    public static bool Differs(VerifiableAddress input, VerifiableAddress standard) =>
        Norm(input.Line1) != Norm(standard.Line1)
        || Norm(input.Line2) != Norm(standard.Line2)
        || Norm(input.City) != Norm(standard.City)
        || Norm(ShippingRulesEngine.NormalizeUsState(input.Region)) != Norm(standard.Region)
        || (SplitZip(input.PostalCode).Zip5 is { } zip && zip != SplitZip(standard.PostalCode).Zip5);

    private static readonly Regex NonWord = new(@"[^A-Z0-9]+", RegexOptions.Compiled);

    private static string Norm(string? s) => NonWord.Replace((s ?? string.Empty).ToUpperInvariant(), " ").Trim();

    public static (string? Zip5, string? Zip4) SplitZip(string? postalCode)
    {
        var digits = new string((postalCode ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            >= 9 => (digits[..5], digits.Substring(5, 4)),
            >= 5 => (digits[..5], null),
            _ => (null, null),
        };
    }

    private static List<string> ReadErrors(string body)
    {
        var reasons = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
                    foreach (var e in errors.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.Object && (Str(e, "title") ?? Str(e, "detail")) is { Length: > 0 } t)
                            reasons.Add(t);
                if (reasons.Count == 0 && Str(error, "message") is { Length: > 0 } m)
                    reasons.Add(m);
            }
        }
        catch (JsonException)
        {
            // Not JSON: no reason to show.
        }
        return reasons.Distinct().ToList();
    }

    private async Task<string?> GetTokenAsync(HttpClient client, string baseUrl, string key, string secret, CancellationToken ct)
    {
        var cacheKey = TokenKey(baseUrl, key, secret);
        if (TokenCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Token;

        try
        {
            using var response = await SendWithRetryAsync(client, () => new HttpRequestMessage(HttpMethod.Post, baseUrl + TokenPath)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = key,
                    ["client_secret"] = secret,
                }),
            }, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("USPS OAuth token (address verification): HTTP {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(token))
                return null;

            var seconds = 3600;
            if (doc.RootElement.TryGetProperty("expires_in", out var e))
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var n)) seconds = n;
                else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var p)) seconds = p;
            }
            TokenCache[cacheKey] = (token!, DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, seconds - 60)));
            return token;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("USPS OAuth token (address verification) failed: {Error}", ex.Message);
            return null;
        }
    }

    private static string TokenKey(string baseUrl, string key, string secret) =>
        $"{baseUrl}|{key}|{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)))}";

    /// <summary>§6.2: one retry on a 5xx or a timeout.</summary>
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client, Func<HttpRequestMessage> build, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var request = build();
            try
            {
                var response = await client.SendAsync(request, ct);
                if ((int)response.StatusCode < 500 || attempt == 2)
                    return response;
                response.Dispose();
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < 2)
            {
                // Timeout: retry once.
            }
        }
    }

    private static AddressVerificationOutcome Unavailable(string reason) => new()
    {
        Status = AddressVerificationStatus.Unavailable,
        Messages = { reason },
    };

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
