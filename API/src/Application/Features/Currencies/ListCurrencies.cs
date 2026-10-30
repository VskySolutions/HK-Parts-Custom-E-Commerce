using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Currencies;

public record ListCurrenciesQuery(bool? EnabledOnly = null) : IRequest<IReadOnlyList<CurrencyDto>>;

public class ListCurrenciesQueryHandler
    : IRequestHandler<ListCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
    private readonly IApplicationDbContext _db;

    public ListCurrenciesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<CurrencyDto>> Handle(ListCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var query = _db.SupportedCurrencies.AsNoTracking();

        if (request.EnabledOnly == true)
            query = query.Where(c => c.IsEnabled);

        var currencies = await query
            .OrderBy(c => c.CurrencyCode)
            .ToListAsync(cancellationToken);

        return currencies.Select(CurrencyDto.From).ToList();
    }
}
