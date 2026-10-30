using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerAuth;

/// <summary>
/// Guest-checkout express registration: creates a ready-to-use customer account from the checkout
/// contact + delivery address, emails a generated password (the account is created already
/// email-verified — possession of the mailbox is proven on first sign-in), and saves the delivery
/// address to the customer's address book so the buyer can sign in, return to checkout and place the
/// order without re-entering anything. Used when a store disallows guest ordering (AC-CHK-003.2).
/// </summary>
public record RegisterCustomerAtCheckoutCommand(
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string AddressLine1,
    string? AddressLine2,
    string? Landmark,
    string City,
    string? StateProvince,
    string? PostalCode,
    string CountryCode,
    string? RecaptchaToken = null) : IRequest<RegisterAtCheckoutResult>;

/// <summary>Minimal result — the frontend only needs the email to show the "check your inbox" prompt.</summary>
public record RegisterAtCheckoutResult(string Email);

public class RegisterCustomerAtCheckoutCommandValidator : AbstractValidator<RegisterCustomerAtCheckoutCommand>
{
    public RegisterCustomerAtCheckoutCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.AddressLine1).NotEmpty().MaximumLength(255);
        RuleFor(x => x.AddressLine2).MaximumLength(255);
        RuleFor(x => x.Landmark).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(120);
        RuleFor(x => x.StateProvince).MaximumLength(120);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.CountryCode).NotEmpty().Length(2);
    }
}

public class RegisterCustomerAtCheckoutCommandHandler : IRequestHandler<RegisterCustomerAtCheckoutCommand, RegisterAtCheckoutResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IEmailEnqueuer _emails;
    private readonly IRecaptchaVerifier _recaptcha;
    private readonly IDateTimeProvider _clock;

    public RegisterCustomerAtCheckoutCommandHandler(
        IApplicationDbContext db,
        IPasswordHasher hasher,
        IEmailEnqueuer emails,
        IRecaptchaVerifier recaptcha,
        IDateTimeProvider clock)
    {
        _db = db;
        _hasher = hasher;
        _emails = emails;
        _recaptcha = recaptcha;
        _clock = clock;
    }

    public async Task<RegisterAtCheckoutResult> Handle(RegisterCustomerAtCheckoutCommand request, CancellationToken cancellationToken)
    {
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.Register, request.RecaptchaToken, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, cancellationToken))
            throw new ConflictException($"An account already exists for '{email}'. Please sign in to finish your order.");

        var username = await GenerateUniqueUsernameAsync(email, cancellationToken);
        var fullName = $"{request.FirstName} {request.LastName}".Trim();
        var password = GeneratePassword();

        // Storefront (guest-checkout) registrations are assigned the Customer system role (seeded at startup).
        var customerRoleId = await _db.Roles
            .Where(r => r.Name == nameof(RoleType.Customer))
            .Select(r => (Guid?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Created already-verified: the password is delivered only to this mailbox, so first sign-in
        // proves ownership — and CustomerLogin requires a verified email.
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = _hasher.Hash(password),
            EmailVerified = true,
            IsActive = true,
            Customer = new Customer
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                PhoneNumber = request.PhoneNumber,
            },
        };
        if (customerRoleId is Guid roleId)
            user.UserRoles.Add(new UserRole { RoleId = roleId, AssignedOnUtc = _clock.UtcNow });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        // Save the delivery address to the new customer's book as their default shipping address.
        _db.CustomerAddresses.Add(new CustomerAddress
        {
            CustomerId = user.Customer.Id,
            AddressType = AddressType.Shipping,
            IsDefault = true,
            Address = new Address
            {
                FirstName = request.FirstName.Trim(),
                LastName = request.LastName.Trim(),
                AddressLine1 = request.AddressLine1.Trim(),
                AddressLine2 = request.AddressLine2,
                Landmark = request.Landmark,
                City = request.City.Trim(),
                StateProvince = request.StateProvince,
                PostalCode = request.PostalCode?.Trim() ?? string.Empty,
                CountryCode = request.CountryCode.Trim().ToUpperInvariant(),
                PhoneNumber = request.PhoneNumber,
            },
        });
        await _db.SaveChangesAsync(cancellationToken);

        var body =
            $"Hi {fullName},\n\n" +
            "Thanks for shopping with us! We've created an account so you can complete your order and track it.\n\n" +
            $"Email: {email}\n" +
            $"Temporary password: {password}\n\n" +
            "Sign in with the password above, return to your cart and place your order — your details are already saved. " +
            "You can change this password any time from your account profile.";

        await _emails.EnqueueAsync(
            "CustomerCheckoutWelcome",
            user.Email,
            fullName,
            "Your account & password to finish your order",
            body,
            NotificationCategory.Transactional,
            cancellationToken: cancellationToken);

        return new RegisterAtCheckoutResult(email);
    }

    /// <summary>
    /// Derives a username from the email local-part, suffixing an incrementing number
    /// (e.g. jane, jane1, jane2, …) until an unused value is found.
    /// </summary>
    private async Task<string> GenerateUniqueUsernameAsync(string email, CancellationToken cancellationToken)
    {
        var baseName = email.Split('@')[0];
        var candidate = baseName;
        var suffix = 1;
        while (await _db.Users.AnyAsync(u => u.Username == candidate, cancellationToken))
            candidate = $"{baseName}{suffix++}";
        return candidate;
    }

    // A random 10-char password that satisfies the shared policy (8–16 chars, at least one letter and
    // one digit). Ambiguous characters (0/O, 1/l/I) are excluded so it's easy to read from the email.
    private static string GeneratePassword()
    {
        const string letters = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
        const string digits = "23456789";
        const string all = letters + digits;

        var chars = new char[10];
        chars[0] = letters[RandomNumberGenerator.GetInt32(letters.Length)];
        chars[1] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        for (var i = 2; i < chars.Length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        // Fisher–Yates shuffle so the guaranteed letter/digit aren't always in the first two slots.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}
