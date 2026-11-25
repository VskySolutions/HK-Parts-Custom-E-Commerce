using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Tax;

/// <summary>
/// Creates or updates the singleton tax configuration (REQ-TAX-002): the tax method and flat rate, plus the PHP
/// "Tax Shipping Amount" switch and Avalara settings. Fields left null keep their saved value, so the Settings and
/// Avalara tabs can each save their own part.
/// </summary>
public record UpdateTaxConfigurationCommand(
    TaxProviderType ActiveProvider,
    decimal FlatRatePercent,
    bool IsEnabled,
    int CacheTtlMinutes,
    bool? TaxShipping = null,
    bool? AvalaraCommitDocuments = null,
    bool? AvalaraLoggingEnabled = null,
    int? AvalaraTimeoutSeconds = null,
    string? AvalaraShippingTaxCode = null) : IRequest<TaxConfigurationDto>;

public class UpdateTaxConfigurationCommandValidator : AbstractValidator<UpdateTaxConfigurationCommand>
{
    public UpdateTaxConfigurationCommandValidator()
    {
        RuleFor(x => x.ActiveProvider).IsInEnum();
        RuleFor(x => x.FlatRatePercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.CacheTtlMinutes).InclusiveBetween(0, 1440);
        RuleFor(x => x.AvalaraTimeoutSeconds).InclusiveBetween(1, 120).When(x => x.AvalaraTimeoutSeconds.HasValue)
            .WithName("Request Timeout");
        RuleFor(x => x.AvalaraShippingTaxCode).MaximumLength(50).WithName("Default Shipping Tax Code");
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
        if (request.TaxShipping is bool taxShipping) config.TaxShipping = taxShipping;
        if (request.AvalaraCommitDocuments is bool commit) config.AvalaraCommitDocuments = commit;
        if (request.AvalaraLoggingEnabled is bool logging) config.AvalaraLoggingEnabled = logging;
        if (request.AvalaraTimeoutSeconds is int timeout) config.AvalaraTimeoutSeconds = timeout;
        if (request.AvalaraShippingTaxCode is not null)
            config.AvalaraShippingTaxCode = string.IsNullOrWhiteSpace(request.AvalaraShippingTaxCode) ? null : request.AvalaraShippingTaxCode.Trim();

        await _db.SaveChangesAsync(cancellationToken);
        var dto = TaxConfigurationDto.From(config);
        await GetTaxConfigurationQueryHandler.AttachAvalaraStatusAsync(_db, dto, cancellationToken);
        return dto;
    }
}
