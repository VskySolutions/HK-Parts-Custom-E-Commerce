using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>
/// A selectable storefront currency, trimmed to the fields a buyer's client needs to render a
/// converted price: ISO code, symbol, exchange rate relative to base, and whether it is the base.
/// </summary>
public record StorefrontCurrencyDto(string Code, string Symbol, decimal Rate, bool IsBase);

/// <summary>
/// Lists the enabled display currencies for the public storefront selector (AC-PRP-003.2), projected
/// via <see cref="ICurrencyService"/> to just the fields exposed to anonymous buyers.
/// </summary>
public record GetActiveCurrenciesQuery : IRequest<IReadOnlyList<StorefrontCurrencyDto>>;

public class GetActiveCurrenciesQueryHandler
    : IRequestHandler<GetActiveCurrenciesQuery, IReadOnlyList<StorefrontCurrencyDto>>
{
    private readonly ICurrencyService _currencies;

    public GetActiveCurrenciesQueryHandler(ICurrencyService currencies) => _currencies = currencies;

    public async Task<IReadOnlyList<StorefrontCurrencyDto>> Handle(
        GetActiveCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var active = await _currencies.GetActiveCurrenciesAsync(cancellationToken);

        return active
            .Select(c => new StorefrontCurrencyDto(c.CurrencyCode, c.Symbol, c.ExchangeRate, c.IsBaseCurrency))
            .ToList();
    }
}
