using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ShippingZones;

/// <summary>Updates an existing shipping zone.</summary>
public record UpdateShippingZoneCommand(
    Guid Id,
    string Name,
    string CountryCode,
    string? Region = null,
    string? PostalCodeStart = null,
    string? PostalCodeEnd = null,
    bool IsEnabled = true) : IRequest<ShippingZoneDto>;

public class UpdateShippingZoneCommandValidator : AbstractValidator<UpdateShippingZoneCommand>
{
    public UpdateShippingZoneCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
        RuleFor(x => x.Region).MaximumLength(120);
        RuleFor(x => x.PostalCodeStart).MaximumLength(20);
        RuleFor(x => x.PostalCodeEnd).MaximumLength(20);
    }
}

public class UpdateShippingZoneCommandHandler : IRequestHandler<UpdateShippingZoneCommand, ShippingZoneDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateShippingZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ShippingZoneDto> Handle(UpdateShippingZoneCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.ShippingZones
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingZone), request.Id);

        entity.Name = request.Name;
        entity.CountryCode = request.CountryCode;
        entity.Region = request.Region;
        entity.PostalCodeStart = request.PostalCodeStart;
        entity.PostalCodeEnd = request.PostalCodeEnd;
        entity.IsEnabled = request.IsEnabled;

        await _db.SaveChangesAsync(cancellationToken);
        return ShippingZoneDto.From(entity);
    }
}
