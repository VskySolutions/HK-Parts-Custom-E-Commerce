using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

public record GetBaseCurrencyQuery : IRequest<CurrencyDto>;

public class GetBaseCurrencyQueryHandler : IRequestHandler<GetBaseCurrencyQuery, CurrencyDto>
{
    private readonly IApplicationDbContext _db;

    public GetBaseCurrencyQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CurrencyDto> Handle(GetBaseCurrencyQuery request, CancellationToken cancellationToken)
    {
        var currency = await _db.SupportedCurrencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.IsBaseCurrency, cancellationToken)
            ?? throw new NotFoundException("BaseCurrency", "-");

        return CurrencyDto.From(currency);
    }
}
