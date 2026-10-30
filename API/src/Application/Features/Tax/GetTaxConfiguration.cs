using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>
/// Returns the singleton tax configuration, creating it with defaults (FlatRate, 0%) when none exists
/// yet so the admin UI always has a row to edit (REQ-TAX-002).
/// </summary>
public record GetTaxConfigurationQuery : IRequest<TaxConfigurationDto>;

public class GetTaxConfigurationQueryHandler : IRequestHandler<GetTaxConfigurationQuery, TaxConfigurationDto>
{
    private readonly IApplicationDbContext _db;

    public GetTaxConfigurationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxConfigurationDto> Handle(GetTaxConfigurationQuery request, CancellationToken cancellationToken)
    {
        var config = await _db.TaxProviderConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            config = new TaxProviderConfiguration();
            _db.TaxProviderConfigurations.Add(config);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return TaxConfigurationDto.From(config);
    }
}
