namespace HKPartsEcommerce.Domain.Enums;

/// <summary>Two-tier authorization model (API Server blueprint, System Contracts).</summary>
public enum RoleType
{
    SuperAdmin = 0,
    TenantAdmin = 1,
    Customer = 2
}

/// <summary>Notification category that drives SMTP account resolution and suppression checks.</summary>
public enum NotificationCategory
{
    Transactional = 0,
    Marketing = 1
}

/// <summary>Lifecycle status of a queued email message.</summary>
public enum EmailStatus
{
    Pending = 0,
    Sent = 1,
    Retry = 2,
    Failed = 3,
    Suppressed = 4
}

/// <summary>SMTP transport security mode.</summary>
public enum SmtpEncryptionMode
{
    None = 0,
    Ssl = 1,
    Tls = 2,
    StartTls = 3
}

/// <summary>SMTP authentication method (WO-75 AC-TEN-003.1).</summary>
public enum SmtpAuthMethod
{
    Auto = 0,
    Login = 1,
    Plain = 2,
    CramMd5 = 3,
    OAuth2 = 4
}

/// <summary>File storage backend. Local filesystem is the default (File Storage ADR-002).</summary>
public enum FileStorageProvider
{
    LocalFilesystem = 0,
    AzureBlobStorage = 1
}
