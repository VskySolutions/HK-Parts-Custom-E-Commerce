namespace HKPartsEcommerce.Domain.Entities;

/// <summary>Join entity for the many-to-many User ⇄ Role relationship.</summary>
public class UserRole
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTime AssignedOnUtc { get; set; }

    public User? User { get; set; }
    public Role? Role { get; set; }
}
