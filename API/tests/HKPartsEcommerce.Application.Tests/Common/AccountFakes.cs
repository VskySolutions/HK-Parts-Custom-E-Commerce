using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Tests.Common;

/// <summary>Deterministic test doubles for the account/password handler dependencies.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => "hashed:" + password;
    public bool Verify(string hashedPassword, string providedPassword) => hashedPassword == "hashed:" + providedPassword;
}

internal sealed class FakeJwtTokenService : IJwtTokenService
{
    // Raw token = a fresh guid; "hash" = a stable transform so ResetPassword can look it up by hash.
    public (string token, DateTime expiresAtUtc) CreateAccessToken(
        User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> accessibleModules)
        => ("access-token", DateTime.UtcNow.AddHours(1));

    public string GenerateRefreshToken() => Guid.NewGuid().ToString("n");
    public string HashRefreshToken(string refreshToken) => "tokenhash:" + refreshToken;
    public int RefreshTokenDays => 7;
}

internal sealed class FixedClock : IDateTimeProvider
{
    public FixedClock(DateTime utcNow) => UtcNow = utcNow;
    public DateTime UtcNow { get; set; }
}

internal sealed class FakeEmailEnqueuer : IEmailEnqueuer
{
    public int SentCount { get; private set; }
    public string? LastTemplateKey { get; private set; }
    public string? LastRecipient { get; private set; }
    public string? LastBody { get; private set; }

    public Task EnqueueAsync(
        string templateKey, string recipientEmail, string? recipientName, string subject, string body,
        NotificationCategory category = NotificationCategory.Transactional, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        SentCount++;
        LastTemplateKey = templateKey;
        LastRecipient = recipientEmail;
        LastBody = body;
        return Task.CompletedTask;
    }
}

/// <summary>Test double for <see cref="IEmailTemplateSender"/> — records the last render request.</summary>
internal sealed class FakeEmailTemplateSender : IEmailTemplateSender
{
    public int SentCount { get; private set; }
    public string? LastTemplateKey { get; private set; }
    public string? LastRecipient { get; private set; }
    public IReadOnlyDictionary<string, string>? LastVariables { get; private set; }

    public Task<bool> SendAsync(
        string templateKey, string recipientEmail, string? recipientName,
        IReadOnlyDictionary<string, string> variables, CancellationToken cancellationToken = default)
    {
        SentCount++;
        LastTemplateKey = templateKey;
        LastRecipient = recipientEmail;
        LastVariables = variables;
        return Task.FromResult(true);
    }
}

internal sealed class FakeUrlBuilder : IStorefrontUrlBuilder
{
    public string EmailVerificationUrl(string token) => "https://app.test/shop/verify-email?token=" + token;
    public string PasswordResetUrl(string token) => "https://app.test/shop/reset-password?token=" + token;
    public string AdminPasswordResetUrl(string token) => "https://app.test/auth/reset-password?token=" + token;
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; }
    public string? Email { get; set; }
    public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsAuthenticated => UserId is not null;
    public string? CorrelationId => null;
    public string? IpAddress => null;
}
