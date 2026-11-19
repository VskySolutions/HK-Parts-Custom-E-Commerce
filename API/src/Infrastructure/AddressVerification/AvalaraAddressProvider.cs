using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.AddressVerification;

/// <summary>
/// Avalara AvaTax v2 <c>POST /api/v2/addresses/resolve</c> (AV-21), replacing the AvaTax 1.0
/// <c>address/validate</c> call. Basic auth with the account id and licence key of the active Avalara credential
/// (tax credentials screen, AD-62). The request is JSON, so customer input is always encoded (AV-22).
/// Field names follow the AvaTax v2 REST reference as understood at build time; confirm before go-live (§9).
/// </summary>
public class AvalaraAddressProvider : IAddressVerificationProvider
{
    private const string ResolvePath = "/api/v2/addresses/resolve";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<AvalaraAddressProvider> _logger;

    public AvalaraAddressProvider(IHttpClientFactory httpClientFactory, IApplicationDbContext db, ILogger<AvalaraAddressProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public AddressVerificationProvider Provider => AddressVerificationProvider.Avalara;

    public Task<bool> IsConfiguredAsync(CancellationToken ct) =>
        _db.AvalaraCredentials.AsNoTracking().AnyAsync(c => c.Active
            && c.BaseUrl != null && c.AccountId != null && c.LicenseKey != null, ct);

    public async Task<AddressVerificationOutcome> VerifyAsync(VerifiableAddress address, bool includeRaw, CancellationToken ct)
    {
        var credential = await _db.AvalaraCredentials.AsNoTracking()
            .Where(c => c.Active)
            .OrderByDescending(c => c.UpdatedOnUtc)
            .Select(c => new { c.BaseUrl, c.AccountId, c.LicenseKey })
            .FirstOrDefaultAsync(ct);
        if (credential is null || string.IsNullOrWhiteSpace(credential.BaseUrl)
            || string.IsNullOrWhiteSpace(credential.AccountId) || string.IsNullOrWhiteSpace(credential.LicenseKey))
            return Unavailable("Avalara is not configured (no active Avalara credential).");

        var client = _httpClientFactory.CreateClient("avalara-addresses");
        client.Timeout = HttpTimeout;
        var url = credential.BaseUrl!.TrimEnd('/') + ResolvePath;
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.AccountId}:{credential.LicenseKey}"));
        var payload = new
        {
            line1 = address.Line1?.Trim(),
            line2 = string.IsNullOrWhiteSpace(address.Line2) ? null : address.Line2.Trim(),
            city = address.City?.Trim(),
            region = address.Region?.Trim(),
            postalCode = address.PostalCode?.Trim(),
            country = address.CountryCode?.Trim().ToUpperInvariant(),
            textCase = "Upper",
        };

        HttpResponseMessage response;
        try
        {
            response = await UspsAddressProvider.SendWithRetryAsync(client, () =>
            {
                var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(payload) };
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
                message.Headers.TryAddWithoutValidation("X-Avalara-Client", "HKPartsEcommerce; 1.0; AddressVerification; 1.0");
                return message;
            }, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning("Avalara address resolve could not be reached: {Error}", ex.Message);
            return Unavailable("Avalara could not be reached.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Avalara address resolve: HTTP {StatusCode}.", (int)response.StatusCode);
                var failure = response.StatusCode == HttpStatusCode.BadRequest
                    ? new AddressVerificationOutcome { Status = AddressVerificationStatus.Invalid, Messages = ReadMessages(body) }
                    : Unavailable($"Avalara answered HTTP {(int)response.StatusCode}.");
                if (failure.Status == AddressVerificationStatus.Invalid && failure.Messages.Count == 0)
                    failure.Messages.Add("Address not found.");
                failure.RawResponse = includeRaw ? body : null;
                return failure;
            }

            AddressVerificationOutcome outcome;
            try
            {
                outcome = Interpret(address, body);
            }
            catch (JsonException)
            {
                outcome = Unavailable("Avalara returned an unreadable answer.");
            }
            outcome.RawResponse = includeRaw ? body : null;
            return outcome;
        }
    }

    /// <summary>
    /// Reads a resolve answer. An error-severity message (e.g. "The address is not deliverable") or no
    /// validated address = invalid; otherwise valid, or suggested when the validated address differs.
    /// </summary>
    public static AddressVerificationOutcome Interpret(VerifiableAddress input, string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var outcome = new AddressVerificationOutcome();

        var errors = new List<string>();
        if (root.TryGetProperty("messages", out var messages) && messages.ValueKind == JsonValueKind.Array)
            foreach (var m in messages.EnumerateArray())
            {
                var summary = Str(m, "summary") ?? Str(m, "details");
                if (summary is null) continue;
                if (string.Equals(Str(m, "severity"), "Error", StringComparison.OrdinalIgnoreCase))
                    errors.Add(summary);
                else
                    outcome.Messages.Add(summary);
            }

        JsonElement? validated = null;
        if (root.TryGetProperty("validatedAddresses", out var list) && list.ValueKind == JsonValueKind.Array)
            foreach (var v in list.EnumerateArray())
            {
                validated = v;
                break;
            }

        if (errors.Count > 0 || validated is null)
        {
            outcome.Status = AddressVerificationStatus.Invalid;
            outcome.Messages.InsertRange(0, errors.Count > 0 ? errors : new List<string> { "Address not found." });
            return outcome;
        }

        var va = validated.Value;
        var suggestion = new VerifiableAddress(
            Str(va, "line1"), Str(va, "line2"), Str(va, "city"), Str(va, "region"), Str(va, "postalCode"),
            Str(va, "country") ?? input.CountryCode);
        outcome.Suggestion = suggestion;
        outcome.IsPoBox = string.Equals(Str(va, "addressType"), "POBoxAddress", StringComparison.OrdinalIgnoreCase);
        outcome.Status = UspsAddressProvider.Differs(input, suggestion)
            ? AddressVerificationStatus.Suggested
            : AddressVerificationStatus.Valid;
        return outcome;
    }

    private static List<string> ReadMessages(string body)
    {
        var reasons = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                if (error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                    foreach (var d in details.EnumerateArray())
                        if ((Str(d, "description") ?? Str(d, "message")) is { Length: > 0 } text)
                            reasons.Add(text);
                if (reasons.Count == 0 && Str(error, "message") is { Length: > 0 } m)
                    reasons.Add(m);
            }
        }
        catch (JsonException)
        {
            // Not JSON.
        }
        return reasons;
    }

    private static AddressVerificationOutcome Unavailable(string reason) => new()
    {
        Status = AddressVerificationStatus.Unavailable,
        Messages = { reason },
    };

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}
