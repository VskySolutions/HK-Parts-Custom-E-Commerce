using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ContactUsEnquiries;

/// <summary>A merchant-defined free-text contact us enquiries.</summary>
public class ContactUsDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Comments { get; set; }
    public string? FullName { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static ContactUsDto From(ContactUs C) => new()
    {
        Id = C.Id,
        FirstName = C.FirstName,
        LastName = C.LastName,
        Email = C.Email,
        PhoneNumber = C.PhoneNumber,
        Comments = C.Comments,
    };
}
