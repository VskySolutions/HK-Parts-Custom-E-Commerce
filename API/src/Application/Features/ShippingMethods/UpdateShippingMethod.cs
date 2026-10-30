using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ShippingMethods;

/// <summary>Updates a custom shipping method and reconciles its per-zone rate overrides.</summary>
public record UpdateShippingMethodCommand(
    Guid Id,
    string Name,
    ShippingMethodType MethodType,
    decimal? FlatRate = null,
    decimal? FreeShippingThreshold = null,
    string? TiersJson = null,
    int? TransitDays = null,
    bool IsEnabled = true,
    int DisplayOrder = 0,
    List<ShippingMethodZoneRateInput>? ZoneRates = null) : IRequest<ShippingMethodDto>;

public class UpdateShippingMethodCommandValidator : AbstractValidator<UpdateShippingMethodCommand>
{
    public UpdateShippingMethodCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MethodType).IsInEnum();
        RuleFor(x => x.FlatRate).GreaterThanOrEqualTo(0).When(x => x.FlatRate.HasValue);
        RuleFor(x => x.FreeShippingThreshold).GreaterThanOrEqualTo(0).When(x => x.FreeShippingThreshold.HasValue);
        RuleFor(x => x.TransitDays).InclusiveBetween(0, 365).When(x => x.TransitDays.HasValue);
        RuleForEach(x => x.ZoneRates).ChildRules(r =>
        {
            r.RuleFor(z => z.ShippingZoneId).NotEmpty();
            r.RuleFor(z => z.Rate).GreaterThanOrEqualTo(0);
        });
    }
}

public class UpdateShippingMethodCommandHandler : IRequestHandler<UpdateShippingMethodCommand, ShippingMethodDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateShippingMethodCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingMethodDto> Handle(UpdateShippingMethodCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ShippingMethods
            .Include(m => m.ZoneRates)
                .ThenInclude(r => r.ShippingZone)
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingMethod), request.Id);

        var zoneRateInputs = request.ZoneRates ?? new List<ShippingMethodZoneRateInput>();
        var zoneIds = zoneRateInputs.Select(i => i.ShippingZoneId).Distinct().ToList();
        var zones = zoneIds.Count == 0
            ? new List<ShippingZone>()
            : await _db.ShippingZones.Where(z => zoneIds.Contains(z.Id)).ToListAsync(cancellationToken);
        var missing = zoneIds.Except(zones.Select(z => z.Id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException(nameof(ShippingZone), missing[0]);
        var zonesById = zones.ToDictionary(z => z.Id);

        entity.Name = request.Name;
        entity.MethodType = request.MethodType;
        entity.FlatRate = request.FlatRate;
        entity.FreeShippingThreshold = request.FreeShippingThreshold;
        entity.TiersJson = request.TiersJson;
        entity.TransitDays = request.TransitDays;
        entity.IsEnabled = request.IsEnabled;
        entity.DisplayOrder = request.DisplayOrder;

        // Reconcile zone rates to exactly the requested set (drop removed, update kept, add new) so the
        // unique (method, zone) index is never transiently violated within a single SaveChanges.
        var desired = zoneRateInputs
            .GroupBy(i => i.ShippingZoneId)
            .ToDictionary(g => g.Key, g => g.Last().Rate);

        foreach (var existing in entity.ZoneRates.Where(r => !desired.ContainsKey(r.ShippingZoneId)).ToList())
            entity.ZoneRates.Remove(existing);

        foreach (var existing in entity.ZoneRates)
            existing.Rate = desired[existing.ShippingZoneId];

        var present = entity.ZoneRates.Select(r => r.ShippingZoneId).ToHashSet();
        foreach (var kvp in desired.Where(k => !present.Contains(k.Key)))
            entity.ZoneRates.Add(new ShippingMethodZoneRate { ShippingZone = zonesById[kvp.Key], Rate = kvp.Value });

        await _db.SaveChangesAsync(cancellationToken);
        return ShippingMethodDto.From(entity);
    }
}
