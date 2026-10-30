using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Currencies;

/// <summary>Adds a new display currency with an exchange rate relative to the base currency.</summary>
public record CreateCurrencyCommand(string CurrencyCode, string Symbol, decimal ExchangeRate, bool IsEnabled)
    : IRequest<CurrencyDto>;

public class CreateCurrencyCommandValidator : AbstractValidator<CreateCurrencyCommand>
{
    public CreateCurrencyCommandValidator()
    {
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(8);
        RuleFor(x => x.ExchangeRate).GreaterThan(0);
    }
}

public class CreateCurrencyCommandHandler : IRequestHandler<CreateCurrencyCommand, CurrencyDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public CreateCurrencyCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<CurrencyDto> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var code = request.CurrencyCode.ToUpper();

        var exists = await _db.SupportedCurrencies
            .AnyAsync(c => c.CurrencyCode.ToUpper() == code, cancellationToken);
        if (exists)
            throw new ConflictException($"A currency with code \"{code}\" already exists.");

        var entity = new SupportedCurrency
        {
            CurrencyCode = code,
            Symbol = request.Symbol,
            ExchangeRate = request.ExchangeRate,
            IsEnabled = request.IsEnabled,
            IsRateLocked = false,
            IsBaseCurrency = false,
            LastRateUpdatedOnUtc = _clock.UtcNow,
        };

        _db.SupportedCurrencies.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CurrencyDto.From(entity);
    }
}
