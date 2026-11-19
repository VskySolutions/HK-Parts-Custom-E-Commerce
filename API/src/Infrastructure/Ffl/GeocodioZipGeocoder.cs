using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ffl;

namespace HKPartsEcommerce.Infrastructure.Ffl;

/// <summary>
/// Geocodes a US ZIP through Geocodio (FFL-32, the provider the legacy site used). The API key is the
/// <c>Ffl.GeocodioApiKey</c> setting; without one the geocoder reports itself unconfigured and the directory
/// falls back to its own dealer positions. Calls time out after 5 seconds (FFL-130).
/// </summary>
public sealed class GeocodioZipGeocoder : IZipGeocoder
{
    public const string HttpClientName = "geocodio";
    private const string Endpoint = "https://api.geocod.io/v1.7/geocode";

    private readonly IHttpClientFactory _httpFactory;
    private readonly ISettingsService _settings;
    private readonly ILogger<GeocodioZipGeocoder> _logger;

    public GeocodioZipGeocoder(IHttpClientFactory httpFactory, ISettingsService settings, ILogger<GeocodioZipGeocoder> logger)
    {
        _httpFactory = httpFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(await _settings.GetValueAsync(FflSettingsStore.GeocodioApiKeyKey, ct));

    public async Task<GeoPoint?> GeocodeZipAsync(string zip5, CancellationToken ct)
    {
        var key = await _settings.GetValueAsync(FflSettingsStore.GeocodioApiKeyKey, ct);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        var client = _httpFactory.CreateClient(HttpClientName);
        var url = $"{Endpoint}?postal_code={Uri.EscapeDataString(zip5)}&country=US&limit=1&api_key={Uri.EscapeDataString(key.Trim())}";
        using var response = await client.GetAsync(url, timeout.Token);

        // 422 = Geocodio could not parse / find the ZIP: an unknown ZIP, not an outage.
        if ((int)response.StatusCode == 422)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Geocodio returned {Status} for ZIP {Zip}.", (int)response.StatusCode, zip5);
            throw new HttpRequestException($"Geocodio returned {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var r in results.EnumerateArray())
        {
            if (r.TryGetProperty("location", out var loc)
                && TryNumber(loc, "lat", out var lat)
                && TryNumber(loc, "lng", out var lng))
                return new GeoPoint(lat, lng);
        }
        return null;
    }

    private static bool TryNumber(JsonElement obj, string name, out double value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind == JsonValueKind.Number
            ? p.TryGetDouble(out value)
            : p.ValueKind == JsonValueKind.String && double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
