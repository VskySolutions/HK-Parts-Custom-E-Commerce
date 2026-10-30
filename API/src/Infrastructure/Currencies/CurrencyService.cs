using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Currencies;

namespace HKPartsEcommerce.Infrastructure.Currencies;

/// <summary>
/// Converts base-currency amounts into a buyer's chosen display currency and lists the active
/// currencies for the storefront selector (REQ-PRP-003, AC-PRP-003.2). Rates live on
/// <see cref="HKPartsEcommerce.Domain.Entities.SupportedCurrency"/> relative to the base currency (base rate = 1),
/// so conversion is a single multiply by the target's rate; manual edits and scheduled auto-refresh
/// (<see cref="ICurrencyRateRefresher"/>) keep those rates current.
/// </summary>
public class CurrencyService : ICurrencyService
{
    private readonly IApplicationDbContext _db;

    public CurrencyService(IApplicationDbContext db) => _db = db;

    public async Task<decimal> ConvertAsync(decimal amount, string toCurrencyCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toCurrencyCode))
            throw new NotFoundException("Currency", toCurrencyCode ?? "-");

        var code = toCurrencyCode.Trim().ToUpper();

        var target = await _db.SupportedCurrencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CurrencyCode.ToUpper() == code, ct)
            ?? throw new NotFoundException("Currency", toCurrencyCode);

        // The base currency is the numéraire (rate pinned to 1), so converting to it is a no-op. Every
        // other currency's stored rate is "units of the target per 1 base unit" (AC-PRP-003.1/.2).
        var rate = target.IsBaseCurrency ? 1m : target.ExchangeRate;
        if (rate <= 0m)
            rate = 1m; // defensive: a non-positive rate would otherwise zero out every price

        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<IReadOnlyList<CurrencyDto>> GetActiveCurrenciesAsync(CancellationToken ct = default)
    {
        var currencies = await _db.SupportedCurrencies
            .AsNoTracking()
            .Where(c => c.IsEnabled)
            .OrderByDescending(c => c.IsBaseCurrency)
            .ThenBy(c => c.CurrencyCode)
            .ToListAsync(ct);

        return currencies.Select(CurrencyDto.From).ToList();
    }
}
