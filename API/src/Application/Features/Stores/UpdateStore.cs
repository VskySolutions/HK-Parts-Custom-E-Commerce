using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Stores;

/// <summary>Updates the configuration of an existing store.</summary>
public record UpdateStoreCommand(
    Guid Id,
    string Name,
    string? AddressLine1 = null,
    string? AddressLine2 = null,
    string? City = null,
    string? StateProvince = null,
    string? PostalCode = null,
    string? CountryCode = null,
    double? Latitude = null,
    double? Longitude = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? NotificationEmail = null,
    string? OperatingHoursJson = null,
    string TimeZone = "UTC",
    string? CurrencyDisplay = null,
    bool IsEnabled = true,
    bool MaintenanceMode = false,
    string? DeliveryZoneJson = null,
    int? OrderCapacityLimit = null,
    bool GuestOrderingEnabled = true,
    bool CashOnDeliveryEnabled = true,
    bool BankTransferEnabled = true,
    bool PickupEnabled = false,
    string? Landmark = null) : IRequest<StoreDto>;

public class UpdateStoreCommandValidator : AbstractValidator<UpdateStoreCommand>
{
    public UpdateStoreCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.NotificationEmail).NotEmpty().MaximumLength(512);
        RuleFor(x => x.Landmark).MaximumLength(200);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(64);
        RuleFor(x => x.CountryCode).MaximumLength(2);
        RuleFor(x => x.CurrencyDisplay).MaximumLength(3);
        RuleFor(x => x.Latitude).InclusiveBetween(-90d, 90d).When(x => x.Latitude.HasValue);
        RuleFor(x => x.Longitude).InclusiveBetween(-180d, 180d).When(x => x.Longitude.HasValue);
    }
}

public class UpdateStoreCommandHandler : IRequestHandler<UpdateStoreCommand, StoreDto>
{
    private readonly IApplicationDbContext _db;

    public UpdateStoreCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<StoreDto> Handle(UpdateStoreCommand request, CancellationToken cancellationToken)
    {
        var entity = await _db.Stores
            .Include(s => s.Address)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Store), request.Id);

        entity.Name = request.Name;
        StoreAddress.ApplyUpdate(entity, request);
        entity.ContactEmail = request.ContactEmail;
        entity.ContactPhone = request.ContactPhone;
        entity.NotificationEmail = request.NotificationEmail;
        entity.OperatingHoursJson = request.OperatingHoursJson;
        entity.TimeZone = request.TimeZone;
        entity.CurrencyDisplay = request.CurrencyDisplay;
        entity.IsEnabled = request.IsEnabled;
        entity.MaintenanceMode = request.MaintenanceMode;
        entity.GuestOrderingEnabled = request.GuestOrderingEnabled;
        entity.CashOnDeliveryEnabled = request.CashOnDeliveryEnabled;
        entity.BankTransferEnabled = request.BankTransferEnabled;
        entity.PickupEnabled = request.PickupEnabled;
        entity.DeliveryZoneJson = request.DeliveryZoneJson;
        entity.OrderCapacityLimit = request.OrderCapacityLimit;

        await _db.SaveChangesAsync(cancellationToken);
        return StoreDto.From(entity);
    }
}
