using System.ComponentModel.DataAnnotations.Schema;
using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A physical/fulfilment store location used by the order routing engine
/// (Store Management blueprint).
/// </summary>
public class Store : AuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    // Postal location — a shared Address row (WO: address centralization).
    public Guid? AddressId { get; set; }
    public Address? Address { get; set; }

    // Read-through helpers over the linked address (require Address to be Include()d; never mapped to columns).
    [NotMapped] public string? AddressLine1 => Address?.AddressLine1;
    [NotMapped] public string? AddressLine2 => Address?.AddressLine2;
    [NotMapped] public string? Landmark => Address?.Landmark;
    [NotMapped] public string? City => Address?.City;
    [NotMapped] public string? StateProvince => Address?.StateProvince;
    [NotMapped] public string? PostalCode => Address?.PostalCode;
    [NotMapped] public string? CountryCode => Address?.CountryCode;
    [NotMapped] public double? Latitude => Address?.Latitude;
    [NotMapped] public double? Longitude => Address?.Longitude;

    // Business contact (kept on the store, distinct from the postal address).
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }

    /// <summary>
    /// Where operational alerts (new order, etc.) are sent for this store. Accepts one or more addresses
    /// separated by comma/semicolon (a fulfilment team). Falls back to <see cref="ContactEmail"/> when blank.
    /// </summary>
    public string? NotificationEmail { get; set; }
    public string? OperatingHoursJson { get; set; }
    public string TimeZone { get; set; } = "UTC";
    public string? CurrencyDisplay { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool MaintenanceMode { get; set; }
    public string? DeliveryZoneJson { get; set; }
    public int? OrderCapacityLimit { get; set; }

    /// <summary>
    /// Whether unauthenticated (guest) checkout is permitted for orders fulfilled by this store.
    /// Read by the Checkout Orchestrator after routing (WO-50 AC-STR-001.4/5).
    /// </summary>
    public bool GuestOrderingEnabled { get; set; } = true;

    /// <summary>Whether Cash on Delivery is offered at checkout for orders fulfilled by this store.</summary>
    public bool CashOnDeliveryEnabled { get; set; } = true;

    /// <summary>Whether Bank Transfer is offered at checkout for orders fulfilled by this store.</summary>
    public bool BankTransferEnabled { get; set; } = true;

    /// <summary>Whether this store offers pickup-in-store at checkout (REQ-SHP-004).</summary>
    public bool PickupEnabled { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public ICollection<DeliveryZone> DeliveryZones { get; set; } = new List<DeliveryZone>();
}
