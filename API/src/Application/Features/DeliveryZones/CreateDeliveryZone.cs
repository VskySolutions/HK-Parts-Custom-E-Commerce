using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.DeliveryZones;

/// <summary>Creates a delivery zone for a store.</summary>
public record CreateDeliveryZoneCommand(
    Guid StoreId,
    string Name,
    string CountryCode,
    string? Region,
    string? PostalCodeStart,
    string? PostalCodeEnd,
    bool IsActive) : IRequest<DeliveryZoneDto>;

public class CreateDeliveryZoneCommandValidator : AbstractValidator<CreateDeliveryZoneCommand>
{
    public CreateDeliveryZoneCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
    }
}

public class CreateDeliveryZoneCommandHandler : IRequestHandler<CreateDeliveryZoneCommand, DeliveryZoneDto>
{
    private readonly IApplicationDbContext _db;

    public CreateDeliveryZoneCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<DeliveryZoneDto> Handle(CreateDeliveryZoneCommand request, CancellationToken cancellationToken)
    {
        var storeExists = await _db.Stores
            .AnyAsync(s => s.Id == request.StoreId, cancellationToken);

        if (!storeExists)
            throw new NotFoundException(nameof(Store), request.StoreId);

        var entity = new DeliveryZone
        {
            StoreId = request.StoreId,
            Name = request.Name,
            CountryCode = request.CountryCode,
            Region = request.Region,
            PostalCodeStart = request.PostalCodeStart,
            PostalCodeEnd = request.PostalCodeEnd,
            IsActive = request.IsActive,
        };

        _db.DeliveryZones.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return DeliveryZoneDto.From(entity);
    }
}
