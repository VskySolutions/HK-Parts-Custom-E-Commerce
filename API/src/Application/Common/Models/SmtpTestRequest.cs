using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>Parameters for a one-off SMTP connectivity/test-send probe.</summary>
public class SmtpTestRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string FromName { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public SmtpEncryptionMode EncryptionMode { get; init; }
    public SmtpAuthMethod AuthMethod { get; init; }
    public string ToEmail { get; init; } = string.Empty;
}
