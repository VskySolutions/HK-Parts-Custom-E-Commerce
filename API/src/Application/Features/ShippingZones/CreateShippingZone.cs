using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

/// <summary>Creates a shipping zone (AC-SHP-003.5).</summary>
public record CreateShippingZoneCommand(
    string Name,
    string CountryCode,
    string? Region = null,
    string? PostalCodeStart = null,
    string? PostalCodeEnd = null,
    bool IsEnabled = true) : IRequest<ShippingZoneDto>;

public class CreateShippingZoneCommandValidator : AbstractValidator<CreateShippingZoneCommand>
{
    public CreateShippingZoneCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Region).MaximumLength(120);
        RuleFor(x => x.PostalCodeStart).MaximumLength(20);
        RuleFor(x => x.PostalCodeEnd).MaximumLength(20);
    }
}

public class CreateShippingZoneCommandHandler : IRequestHandler<CreateShippingZoneCommand, ShippingZoneDto>
{
    private readonly IApplicationDbContext _db;

    public CreateShippingZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingZoneDto> Handle(CreateShippingZoneCommand request, CancellationToken cancellationToken)
    {
        var entity = new ShippingZone
        {
            Name = request.Name,
            CountryCode = request.CountryCode,
            Region = request.Region,
            PostalCodeStart = request.PostalCodeStart,
            PostalCodeEnd = request.PostalCodeEnd,
            IsEnabled = request.IsEnabled,
        };

        _db.ShippingZones.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ShippingZoneDto.From(entity);
    }
}
