using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.AdminUsers;
using HKPartsEcommerce.Application.Features.Authentication;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Accounts;

/// <summary>
/// DB-backed tests for the password flows an admin/user/customer actually exercises: reset via an
/// emailed token (valid / expired / already-used / missing), self-service change (right vs wrong
/// current password), admin set-password, admin send-reset-link, and the forgot-password no-op that
/// prevents account enumeration.
/// </summary>
public class PasswordFlowTests : CatalogTestBase
{
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeJwtTokenService _tokens = new();
    private readonly FakeUrlBuilder _urls = new();

    private Guid SeedUser(string email = "user@test.com", string password = "OldPass1", bool isActive = true)
    {
        using var db = NewContext();
        var user = new User
        {
            Username = email,
            Email = email,
            PasswordHash = _hasher.Hash(password),
            EmailVerified = true,
            IsActive = isActive,
            Customer = new Customer { FirstName = "First", LastName = "Last" },
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user.Id;
    }

    private void SeedResetToken(Guid userId, string rawToken, DateTime expiresOnUtc, DateTime? consumedOnUtc = null)
    {
        using var db = NewContext();
        db.UserTokens.Add(new UserToken
        {
            UserId = userId,
            Purpose = UserTokenPurpose.PasswordReset,
            TokenHash = _tokens.HashRefreshToken(rawToken),
            CreatedOnUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresOnUtc = expiresOnUtc,
            ConsumedOnUtc = consumedOnUtc,
        });
        db.SaveChanges();
    }

    // ---- Reset password (admin flow; identical logic to the customer flow) ----------------------

    [Fact]
    public async Task Reset_with_valid_token_sets_password_and_consumes_token()
    {
        var now = new DateTime(2026, 07, 09, 12, 0, 0, DateTimeKind.Utc);
        var userId = SeedUser();
        SeedResetToken(userId, "raw-token", now.AddMinutes(30));

        using (var db = NewContext())
        {
            var handler = new AdminResetPasswordCommandHandler(db, _hasher, _tokens, new FixedClock(now));
            await handler.Handle(new AdminResetPasswordCommand("raw-token", "NewPass123"), CancellationToken.None);
        }

        using var verify = NewContext();
        var user = await verify.Users.FirstAsync(u => u.Id == userId);
        Assert.True(_hasher.Verify(user.PasswordHash, "NewPass123"));
        var token = await verify.UserTokens.FirstAsync(t => t.UserId == userId);
        Assert.NotNull(token.ConsumedOnUtc);
    }

    [Fact]
    public async Task Reset_with_expired_token_throws()
    {
        var now = new DateTime(2026, 07, 09, 12, 0, 0, DateTimeKind.Utc);
        var userId = SeedUser();
        SeedResetToken(userId, "raw-token", now.AddMinutes(-1)); // already expired

        using var db = NewContext();
        var handler = new AdminResetPasswordCommandHandler(db, _hasher, _tokens, new FixedClock(now));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AdminResetPasswordCommand("raw-token", "NewPass123"), CancellationToken.None));
    }

    [Fact]
    public async Task Reset_with_already_used_token_throws()
    {
        var now = new DateTime(2026, 07, 09, 12, 0, 0, DateTimeKind.Utc);
        var userId = SeedUser();
        SeedResetToken(userId, "raw-token", now.AddMinutes(30), consumedOnUtc: now.AddMinutes(-2));

        using var db = NewContext();
        var handler = new AdminResetPasswordCommandHandler(db, _hasher, _tokens, new FixedClock(now));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AdminResetPasswordCommand("raw-token", "NewPass123"), CancellationToken.None));
    }

    [Fact]
    public async Task Reset_with_unknown_token_throws()
    {
        using var db = NewContext();
        var handler = new AdminResetPasswordCommandHandler(db, _hasher, _tokens, new FixedClock(DateTime.UtcNow));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new AdminResetPasswordCommand("does-not-exist", "NewPass123"), CancellationToken.None));
    }

    // ---- Change own password --------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_with_correct_current_updates_hash()
    {
        var userId = SeedUser(password: "OldPass1");
        var current = new FakeCurrentUser { UserId = userId };

        using (var db = NewContext())
        {
            var handler = new ChangePasswordCommandHandler(db, current, _hasher);
            await handler.Handle(new ChangePasswordCommand("OldPass1", "NewPass123"), CancellationToken.None);
        }

        using var verify = NewContext();
        var user = await verify.Users.FirstAsync(u => u.Id == userId);
        Assert.True(_hasher.Verify(user.PasswordHash, "NewPass123"));
    }

    [Fact]
    public async Task ChangePassword_with_wrong_current_throws_and_keeps_old()
    {
        var userId = SeedUser(password: "OldPass1");
        var current = new FakeCurrentUser { UserId = userId };

        using (var db = NewContext())
        {
            var handler = new ChangePasswordCommandHandler(db, current, _hasher);
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                handler.Handle(new ChangePasswordCommand("WrongPass9", "NewPass123"), CancellationToken.None));
        }

        using var verify = NewContext();
        var user = await verify.Users.FirstAsync(u => u.Id == userId);
        Assert.True(_hasher.Verify(user.PasswordHash, "OldPass1"));
    }

    [Fact]
    public async Task ChangePassword_when_not_authenticated_throws()
    {
        using var db = NewContext();
        var handler = new ChangePasswordCommandHandler(db, new FakeCurrentUser { UserId = null }, _hasher);
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new ChangePasswordCommand("x", "NewPass123"), CancellationToken.None));
    }

    // ---- Admin set-password / send-reset --------------------------------------------------------

    [Fact]
    public async Task SetUserPassword_updates_hash()
    {
        var userId = SeedUser();
        using (var db = NewContext())
        {
            var handler = new SetUserPasswordCommandHandler(db, _hasher);
            await handler.Handle(new SetUserPasswordCommand(userId, "NewPass123"), CancellationToken.None);
        }

        using var verify = NewContext();
        var user = await verify.Users.FirstAsync(u => u.Id == userId);
        Assert.True(_hasher.Verify(user.PasswordHash, "NewPass123"));
    }

    [Fact]
    public async Task SetUserPassword_for_unknown_user_throws()
    {
        using var db = NewContext();
        var handler = new SetUserPasswordCommandHandler(db, _hasher);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SetUserPasswordCommand(Guid.NewGuid(), "NewPass123"), CancellationToken.None));
    }

    [Fact]
    public async Task SendUserPasswordReset_creates_token_and_emails_user()
    {
        var userId = SeedUser(email: "target@test.com");
        var emails = new FakeEmailTemplateSender();

        using (var db = NewContext())
        {
            var handler = new SendUserPasswordResetCommandHandler(db, _tokens, new FixedClock(DateTime.UtcNow), emails, _urls);
            await handler.Handle(new SendUserPasswordResetCommand(userId), CancellationToken.None);
        }

        Assert.Equal(1, emails.SentCount);
        Assert.Equal("target@test.com", emails.LastRecipient);
        Assert.Contains("/auth/reset-password?token=", emails.LastVariables!["resetUrl"]);

        using var verify = NewContext();
        Assert.True(await verify.UserTokens.AnyAsync(t => t.UserId == userId && t.Purpose == UserTokenPurpose.PasswordReset));
    }

    [Fact]
    public async Task SendUserPasswordReset_for_unknown_user_throws()
    {
        var emails = new FakeEmailTemplateSender();
        using var db = NewContext();
        var handler = new SendUserPasswordResetCommandHandler(db, _tokens, new FixedClock(DateTime.UtcNow), emails, _urls);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SendUserPasswordResetCommand(Guid.NewGuid()), CancellationToken.None));
        Assert.Equal(0, emails.SentCount);
    }

    // ---- Forgot password (anti-enumeration) -----------------------------------------------------

    [Fact]
    public async Task ForgotPassword_for_known_active_user_issues_token_and_email()
    {
        var userId = SeedUser(email: "known@test.com");
        var emails = new FakeEmailTemplateSender();

        using (var db = NewContext())
        {
            var handler = new AdminRequestPasswordResetCommandHandler(db, _tokens, new FixedClock(DateTime.UtcNow), emails, _urls);
            await handler.Handle(new AdminRequestPasswordResetCommand("known@test.com"), CancellationToken.None);
        }

        Assert.Equal(1, emails.SentCount);
        using var verify = NewContext();
        Assert.True(await verify.UserTokens.AnyAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task ForgotPassword_for_unknown_email_is_silent_noop()
    {
        var emails = new FakeEmailTemplateSender();
        using var db = NewContext();
        var handler = new AdminRequestPasswordResetCommandHandler(db, _tokens, new FixedClock(DateTime.UtcNow), emails, _urls);

        // Must not throw and must not send — no way to probe whether an email is registered.
        await handler.Handle(new AdminRequestPasswordResetCommand("nobody@test.com"), CancellationToken.None);
        Assert.Equal(0, emails.SentCount);
    }

    [Fact]
    public async Task ForgotPassword_for_inactive_user_is_silent_noop()
    {
        SeedUser(email: "inactive@test.com", isActive: false);
        var emails = new FakeEmailTemplateSender();

        using var db = NewContext();
        var handler = new AdminRequestPasswordResetCommandHandler(db, _tokens, new FixedClock(DateTime.UtcNow), emails, _urls);
        await handler.Handle(new AdminRequestPasswordResetCommand("inactive@test.com"), CancellationToken.None);
        Assert.Equal(0, emails.SentCount);
    }
}
