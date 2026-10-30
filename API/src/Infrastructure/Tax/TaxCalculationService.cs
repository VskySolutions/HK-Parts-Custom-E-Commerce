using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Tax;

/// <summary>
/// Orchestrates tax calculation (REQ-TAX-001): loads the singleton <see cref="TaxProviderConfiguration"/>,
/// resolves the matching <see cref="ITaxProviderClient"/> for the active provider, and caches identical
/// origin/destination/line results for the configured TTL (AC-TAX-001.5). When the active provider is
/// FlatRate the flat rate is computed directly with no external call. When an external provider throws
/// (unreachable, non-success response, or missing credentials) the service falls back to the flat rate,
/// marks the result <see cref="TaxBreakdown.FallbackApplied"/> and raises an admin alert so the order is
/// flagged for tax review (AC-TAX-001.4 / AC-TAX-002.3).
/// </summary>
public class TaxCalculationService : ITaxCalculationService
{
    private const string CacheKeyPrefix = "tax:";

    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<ITaxProviderClient> _providers;
    private readonly IMemoryCache _cache;
    private readonly IAdminAlertService _alerts;
    private readonly ILogger<TaxCalculationService> _logger;

    public TaxCalculationService(
        IApplicationDbContext db,
        IEnumerable<ITaxProviderClient> providers,
        IMemoryCache cache,
        IAdminAlertService alerts,
        ILogger<TaxCalculationService> logger)
    {
        _db = db;
        _providers = providers;
        _cache = cache;
        _alerts = alerts;
        _logger = logger;
    }

    public async Task ReportTransactionAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var config = await LoadConfigurationAsync(cancellationToken);
        var client = _providers.FirstOrDefault(p => p.Provider == config.ActiveProvider);
        if (client is null)
            return;
        await client.ReportTransactionAsync(orderId, cancellationToken);
    }

    public async Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest request, CancellationToken cancellationToken = default)
    {
        // Tax-exempt customers are charged no tax regardless of the active provider (AC-TAX-003.3).
        // Short-circuit before any provider call/fallback so exemption holds even when the provider is down.
        if (request.Exemption?.IsExempt == true)
            return new TaxBreakdown(0m, new List<TaxJurisdiction>(), FallbackApplied: false);

        var config = await LoadConfigurationAsync(cancellationToken);

        // FlatRate is the configured behaviour, not a failure fallback: compute directly, no external call.
        if (config.ActiveProvider == TaxProviderType.FlatRate)
            return ComputeFlatRate(config, request, fallbackApplied: false);

        var cacheKey = BuildCacheKey(config.ActiveProvider, request);
        if (_cache.TryGetValue(cacheKey, out TaxBreakdown? cached) && cached is not null)
            return cached;

        var client = _providers.FirstOrDefault(p => p.Provider == config.ActiveProvider);
        if (client is null)
        {
            return await FallbackAsync(config, request,
                $"No tax provider client is registered for '{config.ActiveProvider}'.", cancellationToken);
        }

        try
        {
            var result = await client.CalculateAsync(request, cancellationToken);

            // Cache only successful provider results; a TTL of 0 disables caching.
            if (config.CacheTtlMinutes > 0)
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(config.CacheTtlMinutes));

            return result;
        }
        catch (Exception ex)
        {
            return await FallbackAsync(config, request, ex.Message, cancellationToken);
        }
    }

    private async Task<TaxProviderConfiguration> LoadConfigurationAsync(CancellationToken ct)
        => await _db.TaxProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
           ?? new TaxProviderConfiguration();

    /// <summary>Applies the flat-rate fallback and raises an admin alert so the order is flagged for review.</summary>
    private async Task<TaxBreakdown> FallbackAsync(
        TaxProviderConfiguration config, TaxCalculationRequest request, string reason, CancellationToken ct)
    {
        _logger.LogWarning(
            "Tax provider '{Provider}' unavailable; applying flat-rate fallback of {Percent}%. Reason: {Reason}",
            config.ActiveProvider, config.FlatRatePercent, reason);

        await _alerts.RaiseAsync(
            "TaxFallback",
            $"Tax provider '{config.ActiveProvider}' unavailable — flat-rate fallback applied",
            $"Tax was calculated using the flat-rate fallback of {config.FlatRatePercent}% because the active " +
            $"provider could not be reached. Affected orders should be reviewed. Reason: {reason}",
            "Warning",
            nameof(TaxCalculationService),
            ct);

        return ComputeFlatRate(config, request, fallbackApplied: true);
    }

    private static TaxBreakdown ComputeFlatRate(
        TaxProviderConfiguration config, TaxCalculationRequest request, bool fallbackApplied)
    {
        var taxableBase = TaxableBase(request);
        var rate = config.FlatRatePercent / 100m;
        var tax = Math.Round(taxableBase * rate, 2, MidpointRounding.AwayFromZero);

        var jurisdictions = new List<TaxJurisdiction>
        {
            new("Flat Rate", "FlatRate", rate, tax),
        };

        return new TaxBreakdown(tax, jurisdictions, fallbackApplied);
    }

    // Tax is charged on the discounted merchandise value (gross line less its allocated discount, never
    // below zero) plus shipping — so an order/product/coupon discount reduces the taxable base.
    private static decimal TaxableBase(TaxCalculationRequest request)
        => request.Lines.Sum(l => Math.Max(0m, l.Amount * l.Quantity - l.DiscountAmount)) + request.ShippingAmount;

    /// <summary>
    /// Deterministic cache key over the active provider plus the origin, destination, taxable lines and
    /// shipping amount, so identical calculations reuse a cached result (AC-TAX-001.5).
    /// </summary>
    private static string BuildCacheKey(TaxProviderType provider, TaxCalculationRequest request)
    {
        var sb = new StringBuilder();
        sb.Append(provider).Append('|');
        AppendAddress(sb, request.Origin);
        sb.Append("=>");
        AppendAddress(sb, request.Destination);
        sb.Append('|');

        foreach (var line in request.Lines.OrderBy(l => l.ProductId))
        {
            sb.Append(line.ProductId).Append(':')
              .Append(line.TaxCategoryCode).Append(':')
              .Append(line.Amount.ToString(CultureInfo.InvariantCulture)).Append(':')
              .Append(line.Quantity).Append(':')
              .Append(line.DiscountAmount.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        sb.Append('|').Append(request.ShippingAmount.ToString(CultureInfo.InvariantCulture));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return CacheKeyPrefix + Convert.ToHexString(hash);
    }

    private static void AppendAddress(StringBuilder sb, TaxAddress address)
        => sb.Append(address.CountryCode).Append(',')
             .Append(address.Region).Append(',')
             .Append(address.PostalCode).Append(',')
             .Append(address.City);
}
