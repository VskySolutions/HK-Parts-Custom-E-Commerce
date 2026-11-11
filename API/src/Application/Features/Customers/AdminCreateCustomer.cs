using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>
/// Admin-created customer: creates the identity (User) + profile (Customer) with the Customer system role.
/// Unlike storefront registration there is no reCAPTCHA, the admin chooses whether the email is already
/// verified and whether the account is active. When the email is NOT marked verified, a single-use
/// 24-hour verification link is emailed (same template as storefront registration).
/// Returns the new <b>Customer.Id</b> (the id used by the admin list/detail/group endpoints), not the User.Id.
/// </summary>
public record AdminCreateCustomerCommand(
    string Email,
    string Password,
    string FirstName,
    string? LastName,
    string? PhoneNumber,
    bool IsActive = true,
    bool EmailVerified = true,
    string? LongText = null,
    bool AllowRestrictedCapacityPurchases = false) : IRequest<Guid>;

public class AdminCreateCustomerCommandValidator : AbstractValidator<AdminCreateCustomerCommand>
{
    public AdminCreateCustomerCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).Password();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).MaximumLength(200);
        RuleFor(x => x.PhoneNumber).MaximumLength(50);
        RuleFor(x => x.LongText).MaximumLength(4000);
    }
}

public class AdminCreateCustomerCommandHandler : IRequestHandler<AdminCreateCustomerCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _templates;
    private readonly IStorefrontUrlBuilder _urls;

    public AdminCreateCustomerCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        IEmailTemplateSender templates,
        IStorefrontUrlBuilder urls)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _templates = templates;
        _urls = urls;
    }

    public async Task<Guid> Handle(AdminCreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException($"A user with email '{email}' already exists.");

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName?.Trim() ?? string.Empty;
        var fullName = $"{firstName} {lastName}".Trim();
        var username = await GenerateUniqueUsernameAsync(email, cancellationToken);

        var customerRoleId = await _db.Roles
            .Where(r => r.Name == nameof(RoleType.Customer))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            EmailVerified = request.EmailVerified,
            IsActive = request.IsActive,
            Customer = new Customer
            {
                FirstName = firstName,
                LastName = lastName,
                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
                LongText = string.IsNullOrWhiteSpace(request.LongText) ? null : request.LongText.Trim(),
                AllowRestrictedCapacityPurchases = request.AllowRestrictedCapacityPurchases,
            },
        };
        if (customerRoleId is Guid roleId)
            user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedOnUtc = _clock.UtcNow });

        // Identity + profile persisted in one transaction.
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // Only unverified accounts need a verification email.
        if (!request.EmailVerified)
        {
            var rawToken = _tokens.GenerateRefreshToken();
            _db.UserTokens.Add(new UserToken
            {
                UserId = user.Id,
                Purpose = UserTokenPurpose.EmailVerification,
                TokenHash = _tokens.HashRefreshToken(rawToken),
                CreatedOnUtc = _clock.UtcNow,
                ExpiresOnUtc = _clock.UtcNow.AddHours(24),
            });
            await _db.SaveChangesAsync(cancellationToken);

            await _templates.SendAsync(
                "account.email-verification",
                user.Email,
                fullName,
                new Dictionary<string, string>
                {
                    ["customerName"] = fullName,
                    ["verificationUrl"] = _urls.EmailVerificationUrl(rawToken),
                },
                cancellationToken);
        }

        // Admin endpoints (detail, group, store credit...) look customers up by Customer.Id, which is
        // different from User.Id — return the profile id so the page can open the new record.
        return user.Customer!.Id;
    }

    private async Task<string> GenerateUniqueUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var baseName = email.Split('@')[0];
        var candidate = baseName;
        var suffix = 1;
        while (await _db.Users.AnyAsync(u => u.Username == candidate, cancellationToken))
            candidate = $"{baseName}{suffix++}";
        return candidate;
    }
}







//using FluentValidation;
//using MediatR;
//using Microsoft.EntityFrameworkCore;
//using HKPartsEcommerce.Application.Common.Exceptions;
//using HKPartsEcommerce.Application.Common.Interfaces;
//using HKPartsEcommerce.Application.Common.Validation;
//using HKPartsEcommerce.Domain.Entities;
//using HKPartsEcommerce.Domain.Enums;

//namespace HKPartsEcommerce.Application.Features.Customers;

///// <summary>
///// Admin-created customer: creates the identity (User) + profile (Customer) with the Customer system role.
///// Unlike storefront registration there is no reCAPTCHA, the admin chooses whether the email is already
///// verified and whether the account is active. When the email is NOT marked verified, a single-use
///// 24-hour verification link is emailed (same template as storefront registration).
///// Returns the new user id (used as the customer id by the admin detail page).
///// </summary>
//public record AdminCreateCustomerCommand(
//    string Email,
//    string Password,
//    string FirstName,
//    string? LastName,
//    string? PhoneNumber,
//    bool IsActive = true,
//    bool EmailVerified = true) : IRequest<Guid>;

//public class AdminCreateCustomerCommandValidator : AbstractValidator<AdminCreateCustomerCommand>
//{
//    public AdminCreateCustomerCommandValidator()
//    {
//        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
//        RuleFor(x => x.Password).Password();
//        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
//        RuleFor(x => x.LastName).MaximumLength(200);
//        RuleFor(x => x.PhoneNumber).MaximumLength(50);
//    }
//}

//public class AdminCreateCustomerCommandHandler : IRequestHandler<AdminCreateCustomerCommand, Guid>
//{
//    private readonly IApplicationDbContext _db;
//    private readonly IPasswordHasher _hasher;
//    private readonly IJwtTokenService _tokens;
//    private readonly IDateTimeProvider _clock;
//    private readonly IEmailTemplateSender _templates;
//    private readonly IStorefrontUrlBuilder _urls;

//    public AdminCreateCustomerCommandHandler(
//        IApplicationDbContext db,
//        IPasswordHasher hasher,
//        IJwtTokenService tokens,
//        IDateTimeProvider clock,
//        IEmailTemplateSender templates,
//        IStorefrontUrlBuilder urls)
//    {
//        _db = db;
//        _hasher = hasher;
//        _tokens = tokens;
//        _clock = clock;
//        _templates = templates;
//        _urls = urls;
//    }

//    public async Task<Guid> Handle(AdminCreateCustomerCommand request, CancellationToken cancellationToken)
//    {
//        var email = request.Email.Trim().ToLowerInvariant();
//        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
//            throw new ConflictException($"A user with email '{email}' already exists.");

//        var firstName = request.FirstName.Trim();
//        var lastName = request.LastName?.Trim() ?? string.Empty;
//        var fullName = $"{firstName} {lastName}".Trim();
//        var username = await GenerateUniqueUsernameAsync(email, cancellationToken);

//        var customerRoleId = await _db.Roles
//            .Where(r => r.Name == nameof(RoleType.Customer))
//            .Select(r => (Guid?)r.Id)
//            .FirstOrDefaultAsync(cancellationToken);

//        var user = new User
//        {
//            Username = username,
//            Email = email,
//            PasswordHash = _hasher.Hash(request.Password),
//            EmailVerified = request.EmailVerified,
//            IsActive = request.IsActive,
//            Customer = new Customer
//            {
//                FirstName = firstName,
//                LastName = lastName,
//                PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
//            },
//        };
//        if (customerRoleId is Guid roleId)
//            user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedOnUtc = _clock.UtcNow });

//        // Identity + profile persisted in one transaction.
//        _db.Users.Add(user);
//        await _db.SaveChangesAsync(cancellationToken);

//        // Only unverified accounts need a verification email.
//        if (!request.EmailVerified)
//        {
//            var rawToken = _tokens.GenerateRefreshToken();
//            _db.UserTokens.Add(new UserToken
//            {
//                UserId = user.Id,
//                Purpose = UserTokenPurpose.EmailVerification,
//                TokenHash = _tokens.HashRefreshToken(rawToken),
//                CreatedOnUtc = _clock.UtcNow,
//                ExpiresOnUtc = _clock.UtcNow.AddHours(24),
//            });
//            await _db.SaveChangesAsync(cancellationToken);

//            await _templates.SendAsync(
//                "account.email-verification",
//                user.Email,
//                fullName,
//                new Dictionary<string, string>
//                {
//                    ["customerName"] = fullName,
//                    ["verificationUrl"] = _urls.EmailVerificationUrl(rawToken),
//                },
//                cancellationToken);
//        }

//        return user.Id;
//    }

//    private async Task<string> GenerateUniqueUsernameAsync(string email, CancellationToken cancellationToken)
//    {
//        var baseName = email.Split('@')[0];
//        var candidate = baseName;
//        var suffix = 1;
//        while (await _db.Users.AnyAsync(u => u.Username == candidate, cancellationToken))
//            candidate = $"{baseName}{suffix++}";
//        return candidate;
//    }
//}
