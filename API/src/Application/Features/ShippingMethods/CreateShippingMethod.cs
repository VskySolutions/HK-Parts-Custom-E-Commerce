using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

/// <summary>Creates a custom shipping method with optional per-zone rate overrides (REQ-SHP-003).</summary>
public record CreateShippingMethodCommand(
    string Name,
    ShippingMethodType MethodType,
    decimal? FlatRate = null,
    decimal? FreeShippingThreshold = null,
    string? TiersJson = null,
    int? TransitDays = null,
    bool IsEnabled = true,
    int DisplayOrder = 0,
    List<ShippingMethodZoneRateInput>? ZoneRates = null,
    bool FlatRatePerItem = false,
    decimal? FflFlatRate = null,
    bool InternationalViaUsps = false,
    decimal? InternationalFallbackRate = null,
    /// <summary>AD-04: added to the method's rate. Null = none.</summary>
    decimal? HandlingFee = null,
    /// <summary>AD-04: the method is offered only when the order subtotal is at least this. Null = no minimum.</summary>
    decimal? MinimumOrderAmount = null,
    /// <summary>AD-04: ISO country codes the method ships to. Null/empty = all countries.</summary>
    List<string>? ShipToCountries = null) : IRequest<ShippingMethodDto>;

public class CreateShippingMethodCommandValidator : AbstractValidator<CreateShippingMethodCommand>
{
    public CreateShippingMethodCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MethodType).IsInEnum();
        RuleFor(x => x.FlatRate).GreaterThanOrEqualTo(0).When(x => x.FlatRate.HasValue);
        RuleFor(x => x.FreeShippingThreshold).GreaterThanOrEqualTo(0).When(x => x.FreeShippingThreshold.HasValue);
        RuleFor(x => x.TransitDays).InclusiveBetween(0, 365).When(x => x.TransitDays.HasValue);
        RuleFor(x => x.FflFlatRate).GreaterThanOrEqualTo(0).When(x => x.FflFlatRate.HasValue);
        RuleFor(x => x.InternationalFallbackRate).GreaterThanOrEqualTo(0).When(x => x.InternationalFallbackRate.HasValue);
        RuleFor(x => x.HandlingFee).GreaterThanOrEqualTo(0).When(x => x.HandlingFee.HasValue);
        RuleFor(x => x.MinimumOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinimumOrderAmount.HasValue);
        RuleForEach(x => x.ShipToCountries).Length(2).WithMessage("Use two-letter country codes, e.g. US.");
        RuleFor(x => x.TiersJson).Must(ShippingMethodRules.BeValidTierJson).WithMessage("Rate tiers are not valid JSON.");
        RuleForEach(x => x.ZoneRates).ChildRules(r =>
        {
            r.RuleFor(z => z.ShippingZoneId).NotEmpty();
            r.RuleFor(z => z.Rate).GreaterThanOrEqualTo(0);
        });
    }
}

public class CreateShippingMethodCommandHandler : IRequestHandler<CreateShippingMethodCommand, ShippingMethodDto>
{
    private readonly IApplicationDbContext _db;

    public CreateShippingMethodCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingMethodDto> Handle(CreateShippingMethodCommand request, CancellationToken cancellationToken)
    {
        var zoneRateInputs = request.ZoneRates ?? new List<ShippingMethodZoneRateInput>();

        // Collapse to one rate per zone (last wins) so the unique (method, zone) index is never violated.
        var desired = zoneRateInputs
            .GroupBy(i => i.ShippingZoneId)
            .ToDictionary(g => g.Key, g => g.Last().Rate);

        // Every referenced zone must exist; keep the resolved entities so the response can echo zone names.
        var zoneIds = desired.Keys.ToList();
        var zones = zoneIds.Count == 0
            ? new List<ShippingZone>()
            : await _db.ShippingZones.Where(z => zoneIds.Contains(z.Id)).ToListAsync(cancellationToken);
        var missing = zoneIds.Except(zones.Select(z => z.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException(nameof(ShippingZone), missing[0]);
        var zonesById = zones.ToDictionary(z => z.Id);

        var entity = new ShippingMethod
        {
            Name = request.Name,
            MethodType = request.MethodType,
            FlatRate = request.FlatRate,
            FreeShippingThreshold = request.FreeShippingThreshold,
            TiersJson = request.TiersJson,
            FlatRatePerItem = request.MethodType == ShippingMethodType.FlatRate && request.FlatRatePerItem,
            FflFlatRate = request.MethodType == ShippingMethodType.SizeBased ? request.FflFlatRate : null,
            InternationalViaUsps = request.MethodType == ShippingMethodType.SizeBased && request.InternationalViaUsps,
            InternationalFallbackRate = request.MethodType == ShippingMethodType.SizeBased && request.InternationalViaUsps
                ? request.InternationalFallbackRate : null,
            TransitDays = request.TransitDays,
            HandlingFee = request.HandlingFee,
            MinimumOrderAmount = request.MinimumOrderAmount,
            ShipToCountries = ShippingMethodRules.JoinCountries(request.ShipToCountries),
            IsEnabled = request.IsEnabled,
            DisplayOrder = request.DisplayOrder,
        };

        foreach (var kvp in desired)
            entity.ZoneRates.Add(new ShippingMethodZoneRate { ShippingZone = zonesById[kvp.Key], Rate = kvp.Value });

        _db.ShippingMethods.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ShippingMethodDto.From(entity);
    }
}
