using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Updates a currency's symbol, rate and flags. The base currency's rate stays fixed at 1 and locked.</summary>
public record UpdateCurrencyCommand(string Code, string Symbol, decimal ExchangeRate, bool IsEnabled, bool IsRateLocked)
    : IRequest<CurrencyDto>;

public class UpdateCurrencyCommandValidator : AbstractValidator<UpdateCurrencyCommand>
{
    public UpdateCurrencyCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().Length(3);
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(8);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
    }
}

public class UpdateCurrencyCommandHandler : IRequestHandler<UpdateCurrencyCommand, CurrencyDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public UpdateCurrencyCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CurrencyDto> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpper();

        var entity = await _db.SupportedCurrencies
            .FirstOrDefaultAsync(c => c.CurrencyCode.ToUpper() == code, cancellationToken)
            ?? throw new NotFoundException("Currency", request.Code);

        var newRate = request.ExchangeRate;
        var newRateLocked = request.IsRateLocked;

        // The base currency is the numéraire: its rate is pinned to 1 and always locked.
        if (entity.IsBaseCurrency)
        {
            newRate = 1m;
            newRateLocked = true;
        }

        if (entity.ExchangeRate != newRate)
            entity.LastRateUpdatedOnUtc = _clock.UtcNow;

        entity.Symbol = request.Symbol;
        entity.ExchangeRate = newRate;
        entity.IsEnabled = request.IsEnabled;
        entity.IsRateLocked = newRateLocked;

        await _db.SaveChangesAsync(cancellationToken);

        return CurrencyDto.From(entity);
    }
}
