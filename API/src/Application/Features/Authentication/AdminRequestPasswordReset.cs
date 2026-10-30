using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Authentication;

/// <summary>
/// Starts the admin/staff password-reset flow from the sign-in page. When an active account matches
/// the email, a single-use, 1-hour reset token is created and a reset email (pointing at the admin
/// SPA's <c>/auth/reset-password</c> route) is enqueued. Always completes without error — even for
/// unknown accounts — so callers cannot probe for registered emails.
/// </summary>
public record AdminRequestPasswordResetCommand(string Email) : IRequest;

public class AdminRequestPasswordResetCommandValidator : AbstractValidator<AdminRequestPasswordResetCommand>
{
    public AdminRequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class AdminRequestPasswordResetCommandHandler : IRequestHandler<AdminRequestPasswordResetCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IJwtTokenService _tokens;
    private readonly IDateTimeProvider _clock;
    private readonly IEmailTemplateSender _templates;
    private readonly IStorefrontUrlBuilder _urls;

    public AdminRequestPasswordResetCommandHandler(
        IApplicationDbContext db,
        IJwtTokenService tokens,
        IDateTimeProvider clock,
        IEmailTemplateSender templates,
        IStorefrontUrlBuilder urls)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
        _templates = templates;
        _urls = urls;
    }

    public async Task Handle(AdminRequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        // Silently no-op for unknown/inactive accounts to avoid disclosing whether the email exists.
        if (user is null)
            return;

        var rawToken = _tokens.GenerateRefreshToken();
        _db.UserTokens.Add(new UserToken
        {
            UserId = user.Id,
            Purpose = UserTokenPurpose.PasswordReset,
            TokenHash = _tokens.HashRefreshToken(rawToken),
            CreatedOnUtc = _clock.UtcNow,
            ExpiresOnUtc = _clock.UtcNow.AddHours(1),
        });
        await _db.SaveChangesAsync(cancellationToken);

        var fullName = user.Customer is null
            ? string.Empty
            : $"{user.Customer.FirstName} {user.Customer.LastName}".Trim();
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName;
        var resetUrl = _urls.AdminPasswordResetUrl(rawToken);
        await _templates.SendAsync(
            "account.password-reset",
            user.Email,
            string.IsNullOrWhiteSpace(fullName) ? null : fullName,
            new Dictionary<string, string>
            {
                ["customerName"] = greeting,
                ["resetUrl"] = resetUrl,
                ["expiryMinutes"] = "60",
            },
            cancellationToken);
    }
}
