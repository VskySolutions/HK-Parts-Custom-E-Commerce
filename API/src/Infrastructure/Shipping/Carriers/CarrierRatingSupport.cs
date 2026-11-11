using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// Plumbing shared by the REST carrier adapters (USPS, UPS, FedEx): the 5 s / one-retry HTTP policy,
/// cache-key hashing, country normalization, weight rounding and package building. Each adapter keeps its
/// own request/response mapping; only the parts the carrier specs word identically live here.
/// </summary>
internal static class CarrierRatingSupport
{
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan RateCacheDuration = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromSeconds(60);

    public const decimal PoundsPerKilogram = 2.20462m;
    public const decimal CentimetresPerInch = 2.54m;

    /// <summary>One package to rate: weight in pounds, dimensions in inches (null = not sent).</summary>
    public sealed record Package(decimal WeightLb, decimal? LengthIn, decimal? WidthIn, decimal? HeightIn)
    {
        public bool HasDimensions => LengthIn is not null && WidthIn is not null && HeightIn is not null;
    }

    /// <summary>
    /// One retry on a 5xx or a client timeout; anything else is returned as is (USPS US-54, UPS UP-55,
    /// FedEx FX-53). <paramref name="build"/> is called per attempt because a request message cannot be resent.
    /// </summary>
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
                // HttpClient timeout — retry once.
            }
        }
    }

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string Truncate(string value) => value.Length > 1000 ? value[..1000] + "…" : value;

    /// <summary>ISO-2 upper case; blank or "USA" reads as "US".</summary>
    public static string NormalizeCountry(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return "US";
        var c = code.Trim().ToUpperInvariant();
        return c is "USA" ? "US" : c;
    }

    /// <summary>Rounded up to 0.1 lb, at least 0.1 lb (UPS UP-20; FedEx uses the same rule).</summary>
    public static decimal RoundWeightUp(decimal pounds) => Math.Max(0.1m, Math.Ceiling(pounds * 10m) / 10m);

    /// <summary>Fixed-point string, invariant culture (carriers that want numbers as strings).</summary>
    public static string Fixed(decimal value, int decimals)
        => Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("F" + decimals, CultureInfo.InvariantCulture);

    /// <summary>
    /// The packages for a shipment (UPS UP-13, FedEx FX-16). With <paramref name="perUnit"/> on and every
    /// line carrying a weight, one package per unit with the product's own dimensions (falling back to the
    /// default package); otherwise one package of the total weight with the request's (cm) or default
    /// dimensions. Weight comes from the lines when they carry any, else from the request's kilograms.
    /// Null when the shipment weighs nothing — the carrier is skipped (UP-19, FX-11).
    /// </summary>
    public static List<Package>? BuildPackages(
        CarrierRateRequest request, bool perUnit,
        (decimal L, decimal W, decimal H)? defaultBox = null, int maxPackages = 200)
    {
        var lines = request.Lines ?? Array.Empty<ShippingRateLine>();
        var lineWeight = lines.Sum(l => (l.WeightLb ?? 0m) * l.Quantity);
        var totalLb = lineWeight > 0m ? lineWeight : request.WeightKg * PoundsPerKilogram;
        if (totalLb <= 0m)
            return null;

        var units = lines.Sum(l => l.Quantity);
        if (perUnit && lines.Count > 0 && lines.All(l => l.WeightLb is > 0m) && units <= maxPackages)
        {
            return lines
                .SelectMany(l =>
                {
                    var hasOwn = l.LengthIn is > 0m && l.WidthIn is > 0m && l.HeightIn is > 0m;
                    var package = hasOwn
                        ? new Package(RoundWeightUp(l.WeightLb!.Value), Round1(l.LengthIn), Round1(l.WidthIn), Round1(l.HeightIn))
                        : new Package(RoundWeightUp(l.WeightLb!.Value), defaultBox?.L, defaultBox?.W, defaultBox?.H);
                    return Enumerable.Repeat(package, l.Quantity);
                })
                .ToList();
        }

        static decimal? Inches(decimal? cm) => cm is > 0m ? Math.Round(cm.Value / CentimetresPerInch, 1) : null;
        var (length, width, height) = request.Length is > 0m && request.Width is > 0m && request.Height is > 0m
            ? (Inches(request.Length), Inches(request.Width), Inches(request.Height))
            : (defaultBox?.L, defaultBox?.W, defaultBox?.H);
        return new List<Package> { new(RoundWeightUp(totalLb), length, width, height) };
    }

    private static decimal? Round1(decimal? value) => value is > 0m ? Math.Round(value.Value, 1) : null;
}
