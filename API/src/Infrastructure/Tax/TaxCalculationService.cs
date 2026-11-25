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
using HKPartsEcommerce.Infrastructure.Common;

namespace HKPartsEcommerce.Infrastructure.Tax;

/// <summary>
/// Orchestrates tax calculation (REQ-TAX-001): loads the singleton <see cref="TaxProviderConfiguration"/>,
/// resolves the tax method — the customer group's override (PHP customer group tax_method) or the store default
/// — and computes tax with it:
/// <list type="bullet">
/// <item><b>None</b>: no tax (PHP "No Tax Charged"; a group set to it is tax exempt).</item>
/// <item><b>FlatRate / ByState / ByZip</b>: computed here from the flat rate or the <c>TaxStateRates</c> /
/// <c>TaxZipRates</c> tables (PHP EcommTax state/zip): rate% × (discounted merchandise [+ shipping when
/// "Tax Shipping Amount" is on]). No rate on file = no tax, as in PHP.</item>
/// <item><b>TaxJar / StripeTax / Avalara</b>: the matching <see cref="ITaxProviderClient"/>, with identical
/// requests cached for the configured TTL (AC-TAX-001.5). When the provider throws the flat rate is applied,
/// the result is marked <see cref="TaxBreakdown.FallbackApplied"/> and an admin alert raised (AC-TAX-001.4).</item>
/// </list>
/// Gift certificate lines never reach the request (the checkout leaves them out), matching PHP's gift
/// certificate deduction.
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
        var client = await ClientForOrderAsync(orderId, cancellationToken);
        if (client is not null)
            await client.ReportTransactionAsync(orderId, cancellationToken);
    }

    public async Task VoidTransactionAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        // A committed document is voided whatever the method is now: the admin may have switched away from
        // Avalara since the order was placed, but the document there still counts as a sale until voided.
        var hasDocument = await _db.Orders.AsNoTracking()
            .AnyAsync(o => o.Id == orderId && o.TaxDocumentCode != null && o.TaxDocumentVoidedOnUtc == null, cancellationToken);
        var client = hasDocument
            ? _providers.FirstOrDefault(p => p.Provider == TaxProviderType.Avalara)
            : await ClientForOrderAsync(orderId, cancellationToken);
        if (client is not null)
            await client.VoidTransactionAsync(orderId, cancellationToken);
    }

    public async Task ReturnTransactionAsync(TaxReturnRequest request, CancellationToken cancellationToken = default)
    {
        // Only a sale committed to Avalara (and not voided since) has a document to return against — whatever the
        // method is now, as for the void.
        var order = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == request.OrderId && o.TaxDocumentCode != null && o.TaxDocumentVoidedOnUtc == null)
            .Select(o => new { o.OrderNumber })
            .FirstOrDefaultAsync(cancellationToken);
        var client = order is null ? null : _providers.FirstOrDefault(p => p.Provider == TaxProviderType.Avalara);
        if (client is null)
            return;
        if (request.Lines.Count == 0 && request.ShippingAmount <= 0m && request.TaxAmount <= 0m)
            return;

        try
        {
            await client.ReturnTransactionAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Avalara return for order {OrderNumber} failed.", order!.OrderNumber);
            await _alerts.RaiseAsync(
                "TaxReturnFailed",
                $"Avalara return for order {order.OrderNumber} failed",
                $"The refund on order {order.OrderNumber} ({request.Reference}) went through, but it could not be recorded " +
                $"in Avalara as a return of {request.TaxAmount:0.00} tax. Record it in the Avalara portal. Reason: {ex.Message}",
                "Warning",
                nameof(TaxCalculationService),
                cancellationToken);
        }
    }

    public async Task<TaxBreakdown> CalculateAsync(TaxCalculationRequest request, CancellationToken cancellationToken = default)
    {
        // Tax-exempt customers are charged no tax regardless of the active provider (AC-TAX-003.3).
        // Short-circuit before any provider call/fallback so exemption holds even when the provider is down.
        if (request.Exemption?.IsExempt == true)
            return NoTax();

        var config = await LoadConfigurationAsync(cancellationToken);
        if (!config.IsEnabled)
            return NoTax();

        var method = await EffectiveMethodAsync(config, request.CustomerGroupId, cancellationToken);
        switch (method)
        {
            case TaxProviderType.None:
                return NoTax();
            // FlatRate is the configured behaviour, not a failure fallback: compute directly, no external call.
            case TaxProviderType.FlatRate:
                return ComputeRate(config, request, config.FlatRatePercent, "Flat Rate", "FlatRate", fallbackApplied: false);
            case TaxProviderType.ByState:
                return await ComputeByStateAsync(config, request, cancellationToken);
            case TaxProviderType.ByZip:
                return await ComputeByZipAsync(config, request, cancellationToken);
        }

        var cacheKey = BuildCacheKey(method, config, request);
        if (_cache.TryGetValue(cacheKey, out TaxBreakdown? cached) && cached is not null)
            return cached;

        var client = _providers.FirstOrDefault(p => p.Provider == method);
        if (client is null)
        {
            return await FallbackAsync(config, method, request,
                $"No tax provider client is registered for '{method}'.", cancellationToken);
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
            return await FallbackAsync(config, method, request, ex.Message, cancellationToken);
        }
    }

    private async Task<TaxProviderConfiguration> LoadConfigurationAsync(CancellationToken ct)
        => await _db.TaxProviderConfigurations.AsNoTracking().FirstOrDefaultAsync(ct)
           ?? new TaxProviderConfiguration();

    /// <summary>PHP EcommTax::getTaxMethod: the customer group's tax method when it sets one, else the store's.</summary>
    private async Task<TaxProviderType> EffectiveMethodAsync(TaxProviderConfiguration config, Guid? groupId, CancellationToken ct)
    {
        if (groupId is Guid id)
        {
            var groupMethod = await _db.CustomerGroups.AsNoTracking()
                .Where(g => g.Id == id && g.IsActive)
                .Select(g => g.TaxMethod)
                .FirstOrDefaultAsync(ct);
            if (groupMethod is TaxProviderType m)
                return m;
        }
        return config.ActiveProvider;
    }

    /// <summary>The provider client for the method that taxed this order (its customer's group, else the store's).</summary>
    private async Task<ITaxProviderClient?> ClientForOrderAsync(Guid orderId, CancellationToken ct)
    {
        var config = await LoadConfigurationAsync(ct);
        var groupId = await _db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId && o.CustomerId != null)
            .Join(_db.Customers.AsNoTracking(), o => o.CustomerId, c => c.Id, (o, c) => c.CustomerGroupId)
            .FirstOrDefaultAsync(ct);
        var method = await EffectiveMethodAsync(config, groupId, ct);
        return _providers.FirstOrDefault(p => p.Provider == method);
    }

    /// <summary>PHP "Tax By State": the ship-to state's rate from <c>TaxStateRates</c>.</summary>
    private async Task<TaxBreakdown> ComputeByStateAsync(TaxProviderConfiguration config, TaxCalculationRequest request, CancellationToken ct)
    {
        var country = string.IsNullOrWhiteSpace(request.Destination.CountryCode) ? "US" : request.Destination.CountryCode.Trim().ToUpperInvariant();
        var state = RegionCodeNormalizer.ToStateCode(country, request.Destination.Region)?.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(state))
            return NoTax();

        var row = await _db.TaxStateRates.AsNoTracking()
            .Where(r => r.CountryCode == country && r.StateCode == state)
            .Select(r => new { r.StateName, r.RatePercent })
            .FirstOrDefaultAsync(ct);
        if (row?.RatePercent is not decimal rate || rate == 0m)
            return NoTax();

        return ComputeRate(config, request, rate, row.StateName, "State", fallbackApplied: false);
    }

    /// <summary>PHP "Tax By Zip": the rate for the first 5 characters of the ship-to postal code from <c>TaxZipRates</c>.</summary>
    private async Task<TaxBreakdown> ComputeByZipAsync(TaxProviderConfiguration config, TaxCalculationRequest request, CancellationToken ct)
    {
        var zip = ZipKey(request.Destination.PostalCode);
        if (zip is null)
            return NoTax();

        var rate = await _db.TaxZipRates.AsNoTracking()
            .Where(r => r.PostalCode == zip)
            .Select(r => (decimal?)r.RatePercent)
            .FirstOrDefaultAsync(ct);
        if (rate is not decimal percent || percent == 0m)
            return NoTax();

        return ComputeRate(config, request, percent, "ZIP " + zip, "Zip", fallbackApplied: false);
    }

    /// <summary>The ZIP rate lookup key: the first 5 characters of the trimmed postal code (PHP substr(zip, 0, 5)).</summary>
    public static string? ZipKey(string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
            return null;
        var trimmed = postalCode.Trim().ToUpperInvariant();
        return trimmed.Length > 5 ? trimmed[..5] : trimmed;
    }

    /// <summary>Applies the flat-rate fallback and raises an admin alert so the order is flagged for review.</summary>
    private async Task<TaxBreakdown> FallbackAsync(
        TaxProviderConfiguration config, TaxProviderType method, TaxCalculationRequest request, string reason, CancellationToken ct)
    {
        _logger.LogWarning(
            "Tax provider '{Provider}' unavailable; applying flat-rate fallback of {Percent}%. Reason: {Reason}",
            method, config.FlatRatePercent, reason);

        await _alerts.RaiseAsync(
            "TaxFallback",
            $"Tax provider '{method}' unavailable — flat-rate fallback applied",
            $"Tax was calculated using the flat-rate fallback of {config.FlatRatePercent}% because the active " +
            $"provider could not be reached. Affected orders should be reviewed. Reason: {reason}",
            "Warning",
            nameof(TaxCalculationService),
            ct);

        return ComputeRate(config, request, config.FlatRatePercent, "Flat Rate", "FlatRate", fallbackApplied: true);
    }

    private static TaxBreakdown NoTax() => new(0m, new List<TaxJurisdiction>(), FallbackApplied: false);

    private static TaxBreakdown ComputeRate(
        TaxProviderConfiguration config, TaxCalculationRequest request, decimal percent, string name, string type, bool fallbackApplied)
    {
        var taxableBase = TaxableBase(request, config.TaxShipping);
        var rate = percent / 100m;
        var tax = Math.Round(taxableBase * rate, 2, MidpointRounding.AwayFromZero);

        var jurisdictions = new List<TaxJurisdiction> { new(name, type, rate, tax) };
        return new TaxBreakdown(tax, jurisdictions, fallbackApplied);
    }

    // Tax is charged on the discounted merchandise value (gross line less its allocated discount, never
    // below zero) plus — when "Tax Shipping Amount" is on — shipping, so a discount reduces the taxable base.
    private static decimal TaxableBase(TaxCalculationRequest request, bool taxShipping)
        => request.Lines.Sum(l => Math.Max(0m, l.Amount * l.Quantity - l.DiscountAmount))
           + (taxShipping ? request.ShippingAmount : 0m);

    /// <summary>
    /// Deterministic cache key over the provider plus the origin, destination, taxable lines, shipping amount and
    /// the provider-relevant extras, so identical calculations reuse a cached result (AC-TAX-001.5).
    /// </summary>
    private static string BuildCacheKey(TaxProviderType provider, TaxProviderConfiguration config, TaxCalculationRequest request)
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
              .Append(line.ProviderTaxCode).Append(':')
              .Append(line.Amount.ToString(CultureInfo.InvariantCulture)).Append(':')
              .Append(line.Quantity).Append(':')
              .Append(line.DiscountAmount.ToString(CultureInfo.InvariantCulture)).Append(';');
        }

        sb.Append('|').Append(request.ShippingAmount.ToString(CultureInfo.InvariantCulture))
          .Append('|').Append(request.ExemptionNumber)
          .Append('|').Append(config.AvalaraShippingTaxCode);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return CacheKeyPrefix + Convert.ToHexString(hash);
    }

    private static void AppendAddress(StringBuilder sb, TaxAddress address)
        => sb.Append(address.CountryCode).Append(',')
             .Append(address.Region).Append(',')
             .Append(address.PostalCode).Append(',')
             .Append(address.City).Append(',')
             .Append(address.Line1);
}
