using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// A named SMTP account. The password is encrypted at rest and never returned in responses
/// (Tenant Management blueprint). At most one enabled account per <see cref="Category"/>.
/// </summary>
public class SmtpAccount : AuditableEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? EncryptedPassword { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public SmtpEncryptionMode EncryptionMode { get; set; } = SmtpEncryptionMode.StartTls;
    public SmtpAuthMethod AuthMethod { get; set; } = SmtpAuthMethod.Auto;
    public NotificationCategory? Category { get; set; }
    public bool Enabled { get; set; } = true;
}
