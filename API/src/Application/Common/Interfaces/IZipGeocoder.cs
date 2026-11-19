namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>A latitude / longitude pair.</summary>
public readonly record struct GeoPoint(double Latitude, double Longitude);

/// <summary>
/// Geocodes a US ZIP code to its centre (FFL-32). The implementation calls the configured geocoding provider
/// (Geocodio); it does not read or write the cache — callers go through <c>FflDirectory.ResolveZipAsync</c>.
/// </summary>
public interface IZipGeocoder
{
    /// <summary>True when a provider is configured (an API key is set).</summary>
    Task<bool> IsConfiguredAsync(CancellationToken ct);

    /// <summary>The ZIP's centre, or null when the provider knows no such ZIP or none is configured.
    /// Throws <see cref="HttpRequestException"/> / <see cref="TaskCanceledException"/> when the provider fails or times out.</summary>
    Task<GeoPoint?> GeocodeZipAsync(string zip5, CancellationToken ct);
}
