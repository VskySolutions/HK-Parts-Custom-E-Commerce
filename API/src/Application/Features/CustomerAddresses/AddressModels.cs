using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerAddresses;

/// <summary>A single saved shipping or billing address in a customer's address book (REQ-CUS-002).</summary>
public class AddressDto
{
    public Guid Id { get; set; }
    public AddressType AddressType { get; set; }
    public bool IsDefault { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string? StateProvince { get; set; }
    public string PostalCode { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    /// <summary>Projects a customer address-book entry (mapping + its shared Address); expects <c>Address</c> loaded.</summary>
    public static AddressDto From(CustomerAddress m) => new()
    {
        Id = m.Id,
        AddressType = m.AddressType,
        IsDefault = m.IsDefault,
        FirstName = m.Address?.FirstName ?? string.Empty,
        LastName = m.Address?.LastName ?? string.Empty,
        Company = m.Address?.Company,
        AddressLine1 = m.Address?.AddressLine1 ?? string.Empty,
        AddressLine2 = m.Address?.AddressLine2,
        Landmark = m.Address?.Landmark,
        City = m.Address?.City ?? string.Empty,
        StateProvince = m.Address?.StateProvince,
        PostalCode = m.Address?.PostalCode ?? string.Empty,
        CountryCode = m.Address?.CountryCode ?? string.Empty,
        PhoneNumber = m.Address?.PhoneNumber,
    };
}

/// <summary>
/// A customer's addresses grouped by type, for checkout address selection (AC-CUS-002.2). Within each
/// group the default address (if any) is listed first.
/// </summary>
public class AddressBookDto
{
    public List<AddressDto> Shipping { get; set; } = new();
    public List<AddressDto> Billing { get; set; } = new();
}
