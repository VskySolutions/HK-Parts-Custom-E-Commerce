using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.DeliveryZones;

/// <summary>Updates an existing delivery zone.</summary>
public record UpdateDeliveryZoneCommand(
    Guid Id,
    string Name,
    string CountryCode,
    string? Region,
    string? PostalCodeStart,
    string? PostalCodeEnd,
    bool IsActive) : IRequest<DeliveryZoneDto>;

public class UpdateDeliveryZoneCommandValidator : AbstractValidator<UpdateDeliveryZoneCommand>
{
    public UpdateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
    }
}

public class UpdateDeliveryZoneCommandHandler : IRequestHandler<UpdateDeliveryZoneCommand, DeliveryZoneDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateDeliveryZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<DeliveryZoneDto> Handle(UpdateDeliveryZoneCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.DeliveryZones
            .FirstOrDefaultAsync(z => z.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DeliveryZone), request.Id);

        entity.Name = request.Name;
        entity.CountryCode = request.CountryCode;
        entity.Region = request.Region;
        entity.PostalCodeStart = request.PostalCodeStart;
        entity.PostalCodeEnd = request.PostalCodeEnd;
        entity.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
        return DeliveryZoneDto.From(entity);
    }
}
