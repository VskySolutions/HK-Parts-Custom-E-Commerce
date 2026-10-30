using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Shipping;

/// <summary>
/// Creates or updates the singleton shipping configuration: the master switch plus the set of enabled rate
/// sources (REQ-SHP-005). Carriers omitted from <paramref name="Carriers"/> are left untouched.
/// </summary>
public record UpdateShippingConfigurationCommand(
    bool IsEnabled,
    ShippingSelectionMode SelectionMode,
    int CostVsSpeedWeight,
    int AssumedTransitDays,
    List<ShippingCarrierSettingInput> Carriers,
    bool PickupEnabled = true) : IRequest<ShippingConfigurationDto>;

public class UpdateShippingConfigurationCommandValidator : AbstractValidator<UpdateShippingConfigurationCommand>
{
    public UpdateShippingConfigurationCommandValidator()
    {
        RuleFor(x => x.SelectionMode).IsInEnum();
        RuleFor(x => x.CostVsSpeedWeight).InclusiveBetween(0, 100);
        RuleFor(x => x.AssumedTransitDays).InclusiveBetween(1, 365);
        RuleFor(x => x.Carriers).NotNull();
        RuleForEach(x => x.Carriers).ChildRules(c =>
        {
            c.RuleFor(x => x.Carrier).IsInEnum();
            c.RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
        });
        RuleFor(x => x.Carriers)
            .Must(list => list == null || list.Select(c => c.Carrier).Distinct().Count() == list.Count)
            .WithMessage("A carrier may only appear once.");
    }
}

public class UpdateShippingConfigurationCommandHandler : IRequestHandler<UpdateShippingConfigurationCommand, ShippingConfigurationDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateShippingConfigurationCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingConfigurationDto> Handle(UpdateShippingConfigurationCommand request, CancellationToken cancellationToken)
    {
        // Singleton: reuse the single row, or create it on first write.
        var config = await _db.ShippingProviderConfigurations
            .Include(c => c.Carriers)
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null)
        {
            config = new ShippingProviderConfiguration();
            _db.ShippingProviderConfigurations.Add(config);
        }

        config.IsEnabled = request.IsEnabled;
        config.SelectionMode = request.SelectionMode;
        config.CostVsSpeedWeight = request.CostVsSpeedWeight;
        config.AssumedTransitDays = request.AssumedTransitDays;
        config.PickupEnabled = request.PickupEnabled;

        foreach (var input in request.Carriers)
        {
            var setting = config.Carriers.FirstOrDefault(s => s.Carrier == input.Carrier);
            if (setting is null)
            {
                setting = new ShippingCarrierSetting { Carrier = input.Carrier };
                config.Carriers.Add(setting);
            }

            setting.IsEnabled = input.IsEnabled;
            setting.DisplayOrder = input.DisplayOrder;
        }

        ShippingConfigurationBuilder.EnsureAllCarriers(config, enabledByDefault: false);

        await _db.SaveChangesAsync(cancellationToken);
        return await ShippingConfigurationBuilder.ToDtoAsync(_db, config, cancellationToken);
    }
}
