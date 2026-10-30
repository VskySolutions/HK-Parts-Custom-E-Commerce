using FluentValidation;
using HKPartsEcommerce.Application.Features.AdminUsers;
using HKPartsEcommerce.Application.Features.Authentication;
using HKPartsEcommerce.Application.Features.CustomerAuth;
using HKPartsEcommerce.Application.Features.CustomerProfile;
using HKPartsEcommerce.Application.Features.Setup;
using Xunit;

namespace HKPartsEcommerce.Application.Tests.Accounts;

/// <summary>
/// Pure (no-DB) tests for the shared password policy (8–16 chars, at least one letter and one
/// number) applied via PasswordRules.Password() across every command that sets a password. These
/// cover the input mistakes a real user/customer makes: too short, too long, missing a digit,
/// missing a letter, all whitespace, empty.
/// </summary>
public class PasswordPolicyTests
{
    // Helper: does <paramref name="validator"/> flag <paramref name="property"/> for this instance?
    private static bool HasError<T>(IValidator<T> validator, T instance, string property)
    {
        var result = validator.Validate(instance);
        return result.Errors.Any(e => e.PropertyName == property);
    }

    public static IEnumerable<object[]> Passwords => new List<object[]>
    {
        // password,           isValid
        new object[] { "abcde12",            false }, // 7 chars — too short
        new object[] { "abcde123",           true  }, // 8 chars — minimum
        new object[] { "abcdefghij123456",   true  }, // 16 chars — maximum
        new object[] { "abcdefghij1234567",  false }, // 17 chars — too long
        new object[] { "abcdefghijkl",       false }, // letters only — no digit
        new object[] { "1234567890",         false }, // digits only — no letter
        new object[] { "        ",           false }, // whitespace only — no letter/digit
        new object[] { "",                   false }, // empty
    };

    [Theory]
    [MemberData(nameof(Passwords))]
    public void CreateUser_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new CreateUserCommand("user@test.com", null, password, "First", "Last", new());
        Assert.Equal(!isValid, HasError(new CreateUserCommandValidator(), cmd, nameof(cmd.Password)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void RegisterCustomer_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new RegisterCustomerCommand("user@test.com", password, "First", "Last", null);
        Assert.Equal(!isValid, HasError(new RegisterCustomerCommandValidator(), cmd, nameof(cmd.Password)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void CustomerResetPassword_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new ResetPasswordCommand("tok", password);
        Assert.Equal(!isValid, HasError(new ResetPasswordCommandValidator(), cmd, nameof(cmd.NewPassword)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void CustomerChangeMyPassword_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new ChangeMyPasswordCommand("current", password);
        Assert.Equal(!isValid, HasError(new ChangeMyPasswordCommandValidator(), cmd, nameof(cmd.NewPassword)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void AdminChangePassword_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new ChangePasswordCommand("current", password);
        Assert.Equal(!isValid, HasError(new ChangePasswordCommandValidator(), cmd, nameof(cmd.NewPassword)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void AdminResetPassword_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new AdminResetPasswordCommand("tok", password);
        Assert.Equal(!isValid, HasError(new AdminResetPasswordCommandValidator(), cmd, nameof(cmd.NewPassword)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void SetUserPassword_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new SetUserPasswordCommand(Guid.NewGuid(), password);
        Assert.Equal(!isValid, HasError(new SetUserPasswordCommandValidator(), cmd, nameof(cmd.NewPassword)));
    }

    [Theory]
    [MemberData(nameof(Passwords))]
    public void CompleteSetup_enforces_password_policy(string password, bool isValid)
    {
        var cmd = new CompleteSetupCommand("admin@test.com", password, "Admin User", "Brand", "USD", "en");
        Assert.Equal(!isValid, HasError(new CompleteSetupCommandValidator(), cmd, nameof(cmd.AdminPassword)));
    }
}
