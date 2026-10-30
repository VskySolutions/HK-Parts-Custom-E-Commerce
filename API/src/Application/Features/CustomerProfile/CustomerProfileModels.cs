using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.CustomerProfile;

/// <summary>
/// The authenticated customer's own profile — combines authentication identity (<see cref="User"/>)
/// with personal profile data (<see cref="Customer"/>) for REQ-CUS-002.
/// </summary>
public class CustomerProfileDto
{
    public Guid CustomerId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }

    /// <summary>Customer's chosen IANA display timezone; null = follow the tenant default.</summary>
    public string? PreferredTimeZone { get; set; }

    public static CustomerProfileDto From(Customer customer, User user) => new()
    {
        CustomerId = customer.Id,
        UserId = user.Id,
        Email = user.Email,
        FirstName = customer.FirstName,
        LastName = customer.LastName,
        PhoneNumber = customer.PhoneNumber,
        EmailVerified = user.EmailVerified,
        PreferredTimeZone = customer.PreferredTimeZone,
    };
}
