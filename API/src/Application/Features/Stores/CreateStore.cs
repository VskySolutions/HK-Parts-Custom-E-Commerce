using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Stores;

/// <summary>Creates a new store/fulfilment location.</summary>
public record CreateStoreCommand(
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

public class CreateStoreCommandValidator : AbstractValidator<CreateStoreCommand>
{
    public CreateStoreCommandValidator()
    {
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

public class CreateStoreCommandHandler : IRequestHandler<CreateStoreCommand, StoreDto>
{
    private readonly IApplicationDbContext _db;

    public CreateStoreCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<StoreDto> Handle(CreateStoreCommand request, CancellationToken cancellationToken)
    {
        var entity = new Store
        {
            Name = request.Name,
            Address = StoreAddress.FromCreate(request),
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            NotificationEmail = request.NotificationEmail,
            OperatingHoursJson = request.OperatingHoursJson,
            TimeZone = request.TimeZone,
            CurrencyDisplay = request.CurrencyDisplay,
            IsEnabled = request.IsEnabled,
            MaintenanceMode = request.MaintenanceMode,
            GuestOrderingEnabled = request.GuestOrderingEnabled,
            CashOnDeliveryEnabled = request.CashOnDeliveryEnabled,
            BankTransferEnabled = request.BankTransferEnabled,
            PickupEnabled = request.PickupEnabled,
            DeliveryZoneJson = request.DeliveryZoneJson,
            OrderCapacityLimit = request.OrderCapacityLimit,
        };

        _db.Stores.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return StoreDto.From(entity);
    }
}
