using HKPartsEcommerce.Domain.Common;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A single-use refresh token. Stored hashed; rotated on redemption and invalidated on logout
/// (Authentication and Authorization blueprint).
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresOnUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime? RevokedOnUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
    public string? CreatedByIp { get; set; }

    public User? User { get; set; }

    public bool IsActive => RevokedOnUtc is null && DateTime.UtcNow < ExpiresOnUtc;
}
