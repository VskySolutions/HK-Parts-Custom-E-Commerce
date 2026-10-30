using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.CustomerProfile;

/// <summary>
/// Changes the authenticated customer's login email. The new address is marked unverified and a fresh
/// email-verification token is issued and enqueued, so the change only takes full effect once the
/// customer confirms the new address (AC-CUS-002.1).
/// </summary>
public record ChangeMyEmailCommand(string NewEmail) : IRequest;

public class ChangeMyEmailCommandValidator : AbstractValidator<ChangeMyEmailCommand>
{
    public ChangeMyEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ChangeMyEmailCommandHandler : IRequestHandler<ChangeMyEmailCommand>
{
    private const string EmailVerificationTemplateKey = "CustomerEmailVerification";
    private const int TokenLifetimeHours = 24;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IJwtTokenService _tokens;
    private readonly IEmailTemplateSender _templates;
    private readonly IDateTimeProvider _clock;
    private readonly IStorefrontUrlBuilder _urls;

    public ChangeMyEmailCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService current,
        IJwtTokenService tokens,
        IEmailTemplateSender templates,
        IDateTimeProvider clock,
        IStorefrontUrlBuilder urls)
    {
        _db = db;
        _current = current;
        _tokens = tokens;
        _templates = templates;
        _clock = clock;
        _urls = urls;
    }

    public async Task Handle(ChangeMyEmailCommand request, CancellationToken cancellationToken)
    {
        if (_current.UserId is null)
            throw new UnauthorizedException();

        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == _current.UserId.Value, cancellationToken)
            ?? throw new ForbiddenAccessException("The current user does not have a customer profile.");

        var user = customer.User!;
        var newEmail = request.NewEmail.Trim().ToLowerInvariant();

        if (string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("The new email must be different from your current email.");

        if (await _db.Users.AnyAsync(u => u.Email == newEmail && u.Id != user.Id, cancellationToken))
            throw new ConflictException($"A user with email '{newEmail}' already exists.");

        user.Email = newEmail;
        user.EmailVerified = false;

        // Issue a single-use verification token (same scheme as refresh tokens: store only the hash).
        var rawToken = _tokens.GenerateRefreshToken();
        var now = _clock.UtcNow;
        _db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.EmailVerification,
            TokenHash = _tokens.HashRefreshToken(rawToken),
            CreatedOnUtc = now,
            ExpiresOnUtc = now.AddHours(TokenLifetimeHours),
        });

        await _db.SaveChangesAsync(cancellationToken);

        var verifyUrl = _urls.EmailVerificationUrl(rawToken);
        var fullName = $"{customer.FirstName} {customer.LastName}".Trim();
        await _templates.SendAsync(
            "account.email-verification",
            newEmail,
            string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            new Dictionary<string, string>
            {
                ["customerName"] = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName,
                ["verificationUrl"] = verifyUrl,
            },
            cancellationToken);
    }
}
