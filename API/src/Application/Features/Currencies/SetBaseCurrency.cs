using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Promotes a currency to base: pins its rate to 1, locks it, and records the "currency.base" setting.</summary>
public record SetBaseCurrencyCommand(string Code) : IRequest<CurrencyDto>;

public class SetBaseCurrencyCommandHandler : IRequestHandler<SetBaseCurrencyCommand, CurrencyDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public SetBaseCurrencyCommandHandler(IApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<CurrencyDto> Handle(SetBaseCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpper();

        var target = await _db.SupportedCurrencies
            .FirstOrDefaultAsync(c => c.CurrencyCode.ToUpper() == code, cancellationToken)
            ?? throw new NotFoundException("Currency", request.Code);

        var currentBases = await _db.SupportedCurrencies
            .Where(c => c.IsBaseCurrency)
            .ToListAsync(cancellationToken);

        foreach (var c in currentBases)
            c.IsBaseCurrency = false;

        target.IsBaseCurrency = true;
        target.ExchangeRate = 1m;
        target.IsRateLocked = true;
        target.IsEnabled = true;

        await _settings.SetAsync("currency.base", target.CurrencyCode, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return CurrencyDto.From(target);
    }
}
