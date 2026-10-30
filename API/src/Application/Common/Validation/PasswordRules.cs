using FluentValidation;

namespace HKPartsEcommerce.Application.Common.Validation;

/// <summary>
/// Single source of truth for the account-password policy shared across every place a password is
/// set (registration, admin user create, reset, change, first-run setup): 8–16 characters with at
/// least one letter and one number. Apply via <c>RuleFor(x =&gt; x.Password).Password()</c>.
/// The frontend mirrors this exactly in <c>WEB/src/validators/index.js</c> (strongPassword / passwordRules).
/// </summary>
public static class PasswordRules
{
    public const int MinLength = 8;
    public const int MaxLength = 16;

    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(MinLength).WithMessage($"Password must be at least {MinLength} characters.")
            .MaximumLength(MaxLength).WithMessage($"Password must be at most {MaxLength} characters.")
            .Matches("[A-Za-z]").WithMessage("Password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one number.");
}
