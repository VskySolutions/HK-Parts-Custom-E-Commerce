using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>Creates or updates the singleton tax configuration (REQ-TAX-002).</summary>
public record UpdateTaxConfigurationCommand(
    TaxProviderType ActiveProvider,
    decimal FlatRatePercent,
    bool IsEnabled,
    int CacheTtlMinutes) : IRequest<TaxConfigurationDto>;

public class UpdateTaxConfigurationCommandValidator : AbstractValidator<UpdateTaxConfigurationCommand>
{
    public UpdateTaxConfigurationCommandValidator()
    {
        RuleFor(x => x.ActiveProvider).IsInEnum();
        RuleFor(x => x.FlatRatePercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.CacheTtlMinutes).InclusiveBetween(0, 1440);
    }
}

public class UpdateTaxConfigurationCommandHandler : IRequestHandler<UpdateTaxConfigurationCommand, TaxConfigurationDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateTaxConfigurationCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<TaxConfigurationDto> Handle(UpdateTaxConfigurationCommand request, CancellationToken cancellationToken)
    {
        // Singleton: reuse the single row, or create it on first write.
        var config = await _db.TaxProviderConfigurations.FirstOrDefaultAsync(cancellationToken);
        if (config is null)
        {
            config = new TaxProviderConfiguration();
            _db.TaxProviderConfigurations.Add(config);
        }

        config.ActiveProvider = request.ActiveProvider;
        config.FlatRatePercent = request.FlatRatePercent;
        config.IsEnabled = request.IsEnabled;
        config.CacheTtlMinutes = request.CacheTtlMinutes;

        await _db.SaveChangesAsync(cancellationToken);
        return TaxConfigurationDto.From(config);
    }
}
