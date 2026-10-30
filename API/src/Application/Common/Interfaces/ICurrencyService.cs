using HKPartsEcommerce.Application.Features.Currencies;

namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>
/// Converts monetary amounts from the base currency into a buyer's chosen display currency and exposes
/// the enabled currencies offered on the storefront (REQ-PRP-003, AC-PRP-003.2). Exchange rates are
/// stored on <see cref="HKPartsEcommerce.Domain.Entities.SupportedCurrency"/> relative to the base currency
/// (base rate = 1), so a conversion is a single multiply by the target currency's active rate. Rates
/// are kept current by manual edits (UpdateCurrency) or scheduled refresh (<see cref="ICurrencyRateRefresher"/>).
/// </summary>
public interface ICurrencyService
{
    /// <summary>
    /// Converts <paramref name="amount"/> (expressed in the base currency) into the currency identified
    /// by <paramref name="toCurrencyCode"/> using that currency's active exchange rate, rounded to two
    /// decimal places for display. Converting to the base currency returns the amount unchanged. Throws
    /// <see cref="Exceptions.NotFoundException"/> when the code does not match a configured currency.
    /// </summary>
    Task<decimal> ConvertAsync(decimal amount, string toCurrencyCode, CancellationToken ct = default);

    /// <summary>
    /// Returns the enabled (active) display currencies — base currency first, then alphabetical by code —
    /// for the storefront currency selector.
    /// </summary>
    Task<IReadOnlyList<CurrencyDto>> GetActiveCurrenciesAsync(CancellationToken ct = default);
}
