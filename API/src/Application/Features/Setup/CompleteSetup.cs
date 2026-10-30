using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Validation;
using HKPartsEcommerce.Application.Features.Authentication;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Setup;

/// <summary>
/// Completes first-run setup: creates the super-admin User + Customer profile, assigns the SuperAdmin
/// role, seeds initial settings and base currency, flips the SetupComplete flag, and auto-logs-in
/// (Platform Foundation blueprint).
/// </summary>
public record CompleteSetupCommand(
    string AdminEmail,
    string AdminPassword,
    string AdminFullName,
    string BrandName,
    string BaseCurrencyCode,
    string DefaultLanguage) : IRequest<AuthResponse>;

public class CompleteSetupCommandValidator : AbstractValidator<CompleteSetupCommand>
{
    public CompleteSetupCommandValidator()
    {
        RuleFor(x => x.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.AdminPassword).Password();
        RuleFor(x => x.AdminFullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BrandName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseCurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.DefaultLanguage).NotEmpty().MaximumLength(10);
    }
}

public class CompleteSetupCommandHandler : IRequestHandler<CompleteSetupCommand, AuthResponse>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUserService _current;

    public CompleteSetupCommandHandler(
        IApplicationDbContext db,
        ISettingsService settings,
        IPasswordHasher hasher,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        ICurrentUserService current)
    {
        _db = db;
        _settings = settings;
        _hasher = hasher;
        _tokens = tokens;
        _clock = clock;
        _current = current;
    }

    public async Task<AuthResponse> Handle(CompleteSetupCommand request, CancellationToken cancellationToken)
    {
        var superAdminName = nameof(RoleType.SuperAdmin);

        var superAdminExists = await _db.UserRoles.AnyAsync(ur => ur.Role!.Name == superAdminName, cancellationToken);
        var completed = await _settings.GetAsync<bool>("setup.completed", cancellationToken);
        if (superAdminExists || completed)
            throw new ConflictException("Setup has already been completed.");

        // Create the super-admin identity (User) + profile (Customer) + role assignment.
        var email = request.AdminEmail.Trim();
        var (firstName, lastName) = SplitName(request.AdminFullName.Trim());
        var user = new User
        {
            Username = email.Split('@')[0],
            Email = email,
            PasswordHash = _hasher.Hash(request.AdminPassword),
            EmailVerified = true,
            IsActive = true,
            Customer = new Customer { FirstName = firstName, LastName = lastName },
        };
        _db.Users.Add(user);

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == superAdminName, cancellationToken)
                   ?? new Role
                   {
                       Name = superAdminName,
                       NormalizedName = superAdminName.ToUpperInvariant(),
                       IsSystemRole = true,
                       Description = "Platform-wide access.",
                   };
        user.UserRoles.Add(new UserRole { Role = role, AssignedOnUtc = _clock.UtcNow });

        // Establish the chosen base currency.
        var code = request.BaseCurrencyCode.Trim().ToUpperInvariant();
        var currencies = await _db.SupportedCurrencies.ToListAsync(cancellationToken);
        foreach (var c in currencies)
            c.IsBaseCurrency = false;

        var baseCurrency = currencies.FirstOrDefault(c => c.CurrencyCode == code);
        if (baseCurrency is null)
        {
            baseCurrency = new SupportedCurrency { CurrencyCode = code, Symbol = code };
            _db.SupportedCurrencies.Add(baseCurrency);
        }
        baseCurrency.IsBaseCurrency = true;
        baseCurrency.ExchangeRate = 1m;
        baseCurrency.IsRateLocked = true;
        baseCurrency.IsEnabled = true;
        baseCurrency.LastRateUpdatedOnUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Persist initial settings (each records change history + invalidates cache).
        await _settings.SetAsync("brand.name", request.BrandName.Trim(), cancellationToken);
        await _settings.SetAsync("currency.base", code, cancellationToken);
        await _settings.SetAsync("localization.default-language", request.DefaultLanguage.Trim(), cancellationToken);
        await _settings.SetAsync("setup.completed", "true", cancellationToken);

        // Auto-login the new super admin.
        var (roles, modules) = AccessScope.From(user.UserRoles);
        var (accessToken, expiresAt) = _tokens.CreateAccessToken(user, roles, modules);
        var refreshToken = _tokens.GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(refreshToken),
            CreatedOnUtc = _clock.UtcNow,
            ExpiresOnUtc = _clock.UtcNow.AddDays(_tokens.RefreshTokenDays),
            CreatedByIp = _current.IpAddress,
        });
        await _db.SaveChangesAsync(cancellationToken);

        return AuthResponseFactory.Create(user, roles, modules, accessToken, expiresAt, refreshToken);
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (string.Empty, string.Empty);
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 1 ? (parts[0], string.Empty) : (parts[0], parts[1]);
    }
}
