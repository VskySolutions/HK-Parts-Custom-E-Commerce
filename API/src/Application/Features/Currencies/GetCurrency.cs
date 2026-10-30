using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

public record GetCurrencyQuery(string Code) : IRequest<CurrencyDto>;

public class GetCurrencyQueryHandler : IRequestHandler<GetCurrencyQuery, CurrencyDto>
{
    private readonly IApplicationDbContext _db;

    public GetCurrencyQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CurrencyDto> Handle(GetCurrencyQuery request, CancellationToken cancellationToken)
    {
        var code = request.Code.ToUpper();

        var currency = await _db.SupportedCurrencies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CurrencyCode.ToUpper() == code, cancellationToken)
            ?? throw new NotFoundException("Currency", request.Code);

        return CurrencyDto.From(currency);
    }
}
