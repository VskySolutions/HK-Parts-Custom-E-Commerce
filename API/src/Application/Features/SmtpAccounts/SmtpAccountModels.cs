using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.SmtpAccounts;

/// <summary>SMTP account view. The password is never returned — only whether one is set.</summary>
public class SmtpAccountDto
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? Username { get; set; }
    public string FromName { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public SmtpEncryptionMode EncryptionMode { get; set; }
    public SmtpAuthMethod AuthMethod { get; set; }
    public NotificationCategory? Category { get; set; }
    public bool Enabled { get; set; }
    public bool HasPassword { get; set; }

    public static SmtpAccountDto From(SmtpAccount a) => new()
    {
        Id = a.Id,
        DisplayName = a.DisplayName,
        Host = a.Host,
        Port = a.Port,
        Username = a.Username,
        FromName = a.FromName,
        FromEmail = a.FromEmail,
        EncryptionMode = a.EncryptionMode,
        AuthMethod = a.AuthMethod,
        Category = a.Category,
        Enabled = a.Enabled,
        HasPassword = !string.IsNullOrEmpty(a.EncryptedPassword),
    };
}
