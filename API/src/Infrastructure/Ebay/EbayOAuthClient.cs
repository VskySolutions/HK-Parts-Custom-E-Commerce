using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>Authorization-code grant against the eBay Identity API, with app credentials from configuration.</summary>
public sealed class EbayOAuthClient : IEbayOAuthClient
{
    private readonly HttpClient _http;
    private readonly EbayOptions _options;
    private readonly IDateTimeProvider _clock;

    public EbayOAuthClient(HttpClient http, IOptions<EbayOptions> options, IDateTimeProvider clock)
    {
        _http = http;
        _options = options.Value;
        _clock = clock;
    }

    public IReadOnlyList<string> Scopes => _options.Scopes;

    public bool IsConfigured(EbayEnvironment environment) => _options.For(environment).IsConfigured;

    public string BuildAuthorizeUrl(EbayEnvironment environment, string state)
    {
        var app = Require(environment);
        return $"{EbayOptions.AuthBase(environment)}/oauth2/authorize"
               + $"?client_id={Uri.EscapeDataString(app.ClientId!)}"
               + $"&redirect_uri={Uri.EscapeDataString(app.RuName!)}"
               + "&response_type=code"
               + $"&scope={Uri.EscapeDataString(string.Join(' ', _options.Scopes))}"
               + $"&state={Uri.EscapeDataString(state)}";
    }

    public Task<EbayTokenResult> ExchangeCodeAsync(EbayEnvironment environment, string code, CancellationToken ct = default)
    {
        var app = Require(environment);
        return RequestTokenAsync(environment, app, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = app.RuName!,
        }, ct);
    }

    public Task<EbayTokenResult> RefreshAsync(EbayEnvironment environment, string refreshToken, CancellationToken ct = default)
    {
        var app = Require(environment);
        return RequestTokenAsync(environment, app, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = string.Join(' ', _options.Scopes),
        }, ct);
    }

    private async Task<EbayTokenResult> RequestTokenAsync(
        EbayEnvironment environment, EbayAppCredentials app, Dictionary<string, string> form, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{EbayOptions.ApiBase(environment)}/identity/v1/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{app.ClientId}:{app.ClientSecret}")));

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // Never echo the raw body: it can contain the submitted code.
            string? reason = null;
            try { reason = JsonDocument.Parse(body).RootElement.GetProperty("error_description").GetString(); }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
            throw new EbayApiException((int)response.StatusCode,
                new[] { new ExternalError("oauth", reason ?? "eBay rejected the token request.") });
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var now = _clock.UtcNow;
        var access = root.GetProperty("access_token").GetString()!;
        var expiresIn = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 7200;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        DateTime? refreshExpires = root.TryGetProperty("refresh_token_expires_in", out var re)
            ? now.AddSeconds(re.GetInt64())
            : null;
        return new EbayTokenResult(access, now.AddSeconds(expiresIn), refresh, refreshExpires);
    }

    private EbayAppCredentials Require(EbayEnvironment environment)
    {
        var app = _options.For(environment);
        if (!app.IsConfigured)
            throw new ServiceUnavailableException(
                $"eBay {environment} app credentials are not configured (Ebay:{environment}:ClientId / ClientSecret / RuName).");
        return app;
    }
}
