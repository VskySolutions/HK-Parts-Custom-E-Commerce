using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Authentication identity — holds login credentials only. Every User has exactly one
/// <see cref="Customer"/> profile (1:1) and zero or more roles via <see cref="UserRole"/>
/// (Database blueprint).
/// </summary>
public class User : AuditableEntity, ISoftDeletable
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginOnUtc { get; set; }

    public bool Deleted { get; set; }
    public DateTime? DeletedOnUtc { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
