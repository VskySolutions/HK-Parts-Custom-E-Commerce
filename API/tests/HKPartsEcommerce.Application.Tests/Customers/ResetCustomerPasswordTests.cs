using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Features.Customers;
using HKPartsEcommerce.Application.Tests.Common;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;
using HKPartsEcommerce.Infrastructure.Security;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Customers;

/// <summary>
/// Admin customer password reset (PUT /api/admin/customers/{id}/password), using the real password hasher:
/// new password verifies, old one no longer does, sessions/reset links are retired, profile is untouched.
/// Backed by the real SQL Server test DB.
/// </summary>
public class ResetCustomerPasswordTests : CatalogTestBase
{
    private static readonly DateTime Now = new(2026, 11, 6, 12, 0, 0, DateTimeKind.Utc);
    private const string OldPassword = "OldPass123";
    private readonly PasswordHasher _hasher = new();

    private Guid SeedCustomer(string email, bool asStaff = false)
    {
        using var db = NewContext();
        Role FindOrCreate(string name) => db.Roles.FirstOrDefault(r => r.Name == name)
            ?? new Role { Name = name, NormalizedName = name.ToUpperInvariant(), IsSystemRole = true };

        var user = new User
        {
            Username = email,
            Email = email,
            PasswordHash = _hasher.Hash(OldPassword),
            EmailVerified = true,
            IsActive = true,
            Customer = new Customer { FirstName = "Pat", LastName = "Reset", PhoneNumber = "5550100", LongText = "keep me" },
        };
        user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.Customer)), AssignedOnUtc = Now });
        if (asStaff)
            user.UserRoles.Add(new UserRole { Role = FindOrCreate(nameof(RoleType.TenantAdmin)), AssignedOnUtc = Now });
        user.RefreshTokens.Add(new RefreshToken { TokenHash = "rt", CreatedOnUtc = Now, ExpiresOnUtc = Now.AddDays(7) });
        db.Users.Add(user);
        db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.PasswordReset,
            TokenHash = "link",
            CreatedOnUtc = Now,
            ExpiresOnUtc = Now.AddHours(1),
        });
        db.SaveChanges();
        return user.Customer!.Id;
    }

    private ResetCustomerPasswordCommandHandler Handler(AppDbContext db) => new(db, _hasher, new FixedClock(Now));

    [Fact]
    public async Task Reset_sets_new_hashed_password_and_old_one_stops_working()
    {
        var id = SeedCustomer("reset1@test.local");

        using (var db = NewContext())
            await Handler(db).Handle(new ResetCustomerPasswordCommand(id, "NewPass456", "NewPass456"), CancellationToken.None);

        using var check = NewContext();
        var c = await check.Customers.Include(x => x.User).SingleAsync(x => x.Id == id);
        Assert.NotEqual("NewPass456", c.User!.PasswordHash);            // never plain text
        Assert.True(_hasher.Verify(c.User.PasswordHash, "NewPass456"));  // new password works
        Assert.False(_hasher.Verify(c.User.PasswordHash, OldPassword));  // old password no longer works
    }

    [Fact]
    public async Task Reset_revokes_sessions_and_retires_reset_links_but_keeps_profile()
    {
        var id = SeedCustomer("reset2@test.local");

        using (var db = NewContext())
            await Handler(db).Handle(new ResetCustomerPasswordCommand(id, "NewPass456", "NewPass456"), CancellationToken.None);

        using var check = NewContext();
        var c = await check.Customers.Include(x => x.User).SingleAsync(x => x.Id == id);
        Assert.All(check.RefreshTokens.Where(r => r.UserId == c.UserId), r => Assert.Equal(Now, r.RevokedOnUtc));
        Assert.All(check.UserTokens.Where(t => t.UserId == c.UserId), t => Assert.Equal(Now, t.ConsumedOnUtc));
        Assert.Equal("Pat", c.FirstName);
        Assert.Equal("5550100", c.PhoneNumber);
        Assert.Equal("keep me", c.LongText);
        Assert.Equal("reset2@test.local", c.User!.Email);
        Assert.True(c.User.IsActive);
    }

    [Fact]
    public async Task Reset_unknown_customer_throws_not_found()
    {
        using var db = NewContext();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Handler(db).Handle(new ResetCustomerPasswordCommand(Guid.NewGuid(), "NewPass456", "NewPass456"), CancellationToken.None));
    }

    [Fact]
    public async Task Reset_staff_account_is_refused_and_password_unchanged()
    {
        var id = SeedCustomer("reset-staff@test.local", asStaff: true);

        using (var db = NewContext())
            await Assert.ThrowsAsync<ConflictException>(() =>
                Handler(db).Handle(new ResetCustomerPasswordCommand(id, "NewPass456", "NewPass456"), CancellationToken.None));

        using var check = NewContext();
        var c = await check.Customers.Include(x => x.User).SingleAsync(x => x.Id == id);
        Assert.True(_hasher.Verify(c.User!.PasswordHash, OldPassword));
    }
}

/// <summary>Input rules for the admin customer password reset (no database needed).</summary>
public class ResetCustomerPasswordValidatorTests
{
    private static readonly ResetCustomerPasswordCommandValidator Validator = new();

    [Fact]
    public void Accepts_matching_password_that_meets_policy()
        => Assert.True(Validator.Validate(new ResetCustomerPasswordCommand(Guid.NewGuid(), "NewPass456", "NewPass456")).IsValid);

    [Theory]
    [InlineData("", "", "NewPassword")]              // empty
    [InlineData("short1", "short1", "NewPassword")]  // < 8
    [InlineData("abcdefghij", "abcdefghij", "NewPassword")]   // no number
    [InlineData("1234567890", "1234567890", "NewPassword")]   // no letter
    [InlineData("Abcdefgh12345678X", "Abcdefgh12345678X", "NewPassword")] // > 16
    [InlineData("NewPass456", "", "ConfirmPassword")]         // confirm empty
    [InlineData("NewPass456", "NewPass457", "ConfirmPassword")] // mismatch
    public void Rejects_invalid_input(string password, string confirm, string property)
    {
        var result = Validator.Validate(new ResetCustomerPasswordCommand(Guid.NewGuid(), password, confirm));
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }

    [Fact]
    public void Error_messages_never_contain_the_password()
    {
        var result = Validator.Validate(new ResetCustomerPasswordCommand(Guid.NewGuid(), "Secret99X", "Secret99Y"));
        Assert.DoesNotContain(result.Errors, e => e.ErrorMessage.Contains("Secret99"));
    }

    [Fact]
    public void Rejects_empty_customer_id()
        => Assert.Contains(Validator.Validate(new ResetCustomerPasswordCommand(Guid.Empty, "NewPass456", "NewPass456")).Errors,
            e => e.PropertyName == "CustomerId");
}
