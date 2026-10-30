using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Stores;

/// <summary>Full configuration view of a store/fulfilment location.</summary>
public class StoreDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? NotificationEmail { get; set; }
    public string? OperatingHoursJson { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string? CurrencyDisplay { get; set; }
    public bool IsEnabled { get; set; }
    public bool MaintenanceMode { get; set; }
    public bool GuestOrderingEnabled { get; set; }
    public bool PickupEnabled { get; set; }
    public bool CashOnDeliveryEnabled { get; set; }
    public bool BankTransferEnabled { get; set; }
    public string? DeliveryZoneJson { get; set; }
    public int? OrderCapacityLimit { get; set; }

    public static StoreDto From(Store s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        AddressLine1 = s.AddressLine1,
        AddressLine2 = s.AddressLine2,
        Landmark = s.Landmark,
        City = s.City,
        StateProvince = s.StateProvince,
        PostalCode = s.PostalCode,
        CountryCode = s.CountryCode,
        Latitude = s.Latitude,
        Longitude = s.Longitude,
        ContactEmail = s.ContactEmail,
        ContactPhone = s.ContactPhone,
        NotificationEmail = s.NotificationEmail,
        OperatingHoursJson = s.OperatingHoursJson,
        TimeZone = s.TimeZone,
        CurrencyDisplay = s.CurrencyDisplay,
        IsEnabled = s.IsEnabled,
        MaintenanceMode = s.MaintenanceMode,
        GuestOrderingEnabled = s.GuestOrderingEnabled,
        PickupEnabled = s.PickupEnabled,
        CashOnDeliveryEnabled = s.CashOnDeliveryEnabled,
        BankTransferEnabled = s.BankTransferEnabled,
        DeliveryZoneJson = s.DeliveryZoneJson,
        OrderCapacityLimit = s.OrderCapacityLimit,
    };
}

/// <summary>Builds/updates a store's linked (centralized) Address row from its command's address fields.</summary>
internal static class StoreAddress
{
    public static Address? FromCreate(CreateStoreCommand r) =>
        Build(r.AddressLine1, r.AddressLine2, r.Landmark, r.City, r.StateProvince, r.PostalCode, r.CountryCode, r.Latitude, r.Longitude);

    public static void ApplyUpdate(Store store, UpdateStoreCommand r)
    {
        var built = Build(r.AddressLine1, r.AddressLine2, r.Landmark, r.City, r.StateProvince, r.PostalCode, r.CountryCode, r.Latitude, r.Longitude);
        if (built is null) { store.Address = null; store.AddressId = null; return; }
        if (store.Address is null) { store.Address = built; return; }
        var a = store.Address;
        a.AddressLine1 = built.AddressLine1;
        a.AddressLine2 = built.AddressLine2;
        a.Landmark = built.Landmark;
        a.City = built.City;
        a.StateProvince = built.StateProvince;
        a.PostalCode = built.PostalCode;
        a.CountryCode = built.CountryCode;
        a.Latitude = built.Latitude;
        a.Longitude = built.Longitude;
    }

    private static Address? Build(
        string? line1, string? line2, string? landmark, string? city, string? state,
        string? postal, string? country, double? lat, double? lng)
    {
        if (string.IsNullOrWhiteSpace(country) && string.IsNullOrWhiteSpace(line1) && string.IsNullOrWhiteSpace(city))
            return null;
        return new Address
        {
            AddressLine1 = line1 ?? string.Empty,
            AddressLine2 = line2,
            Landmark = landmark,
            City = city ?? string.Empty,
            StateProvince = state,
            PostalCode = postal ?? string.Empty,
            CountryCode = (country ?? string.Empty).ToUpperInvariant(),
            Latitude = lat,
            Longitude = lng,
        };
    }
}
