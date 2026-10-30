using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Newsletter;

/// <summary>
/// Public storefront newsletter subscribe (WO-56). <b>Single opt-in</b>: a new or previously-unsubscribed
/// email becomes <see cref="NewsletterSubscriptionStatus.Subscribed"/> immediately (no confirmation link).
/// Idempotent — an already-subscribed email returns success without creating a duplicate or re-sending the
/// welcome email, and this handler never throws for an existing subscriber. Protected by the optional
/// reCAPTCHA <see cref="RecaptchaFormType.Newsletter"/> hook (a no-op when reCAPTCHA is unconfigured).
/// </summary>
public record SubscribeCommand(string Email, string? Source = null, string? RecaptchaToken = null)
    : IRequest<SubscribeResultDto>;

public class SubscribeCommandValidator : AbstractValidator<SubscribeCommand>
{
    public SubscribeCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Source).MaximumLength(100);
    }
}

public class SubscribeCommandHandler : IRequestHandler<SubscribeCommand, SubscribeResultDto>
{
    // Existing seeded Marketing template (DefaultEmailTemplates); recorded as the origin for auditing/
    // suppression. The confirmation body is rendered inline here — no template lookup/rendering needed.
    private const string WelcomeTemplateKey = "newsletter.welcome";

    private readonly IApplicationDbContext _db;
    private readonly IRecaptchaVerifier _recaptcha;
    private readonly IEmailEnqueuer _emails;
    private readonly IDateTimeProvider _clock;

    public SubscribeCommandHandler(
        IApplicationDbContext db,
        IRecaptchaVerifier recaptcha,
        IEmailEnqueuer emails,
        IDateTimeProvider clock)
    {
        _db = db;
        _recaptcha = recaptcha;
        _emails = emails;
        _clock = clock;
    }

    public async Task<SubscribeResultDto> Handle(SubscribeCommand request, CancellationToken cancellationToken)
    {
        await _recaptcha.VerifyOrThrowAsync(RecaptchaFormType.Newsletter, request.RecaptchaToken, cancellationToken);

        var email = request.Email.Trim().ToLowerInvariant();
        var source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim();
        var now = _clock.UtcNow;

        var existing = await _db.CMSNewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.Email == email, cancellationToken);

        // Already actively subscribed -> idempotent no-op: no duplicate row, no repeat email, no error.
        if (existing is { Status: NewsletterSubscriptionStatus.Subscribed })
        {
            return new SubscribeResultDto
            {
                Email = email,
                Status = NewsletterSubscriptionStatus.Subscribed,
                AlreadySubscribed = true,
                Message = "You're already subscribed to our newsletter.",
            };
        }

        if (existing is not null)
        {
            // Re-activate a previously Unsubscribed (or Pending) row.
            existing.Status = NewsletterSubscriptionStatus.Subscribed;
            existing.ConfirmedOnUtc = now;
            existing.UnsubscribedOnUtc = null;
            if (source is not null)
                existing.Source = source;
        }
        else
        {
            existing = new CMSNewsletterSubscription
            {
                Email = email,
                Status = NewsletterSubscriptionStatus.Subscribed,
                ConfirmedOnUtc = now,
                Source = source,
            };
            _db.CMSNewsletterSubscriptions.Add(existing);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Confirmation email — Marketing category, inline plain-text body.
        await _emails.EnqueueAsync(
            WelcomeTemplateKey,
            email,
            null,
            "You're subscribed to our newsletter",
            "Thanks for subscribing! You'll now receive our latest news, offers and product updates.\n\n" +
            "If you didn't request this, you can unsubscribe at any time using the link in our emails.",
            NotificationCategory.Marketing,
            cancellationToken: cancellationToken);

        return new SubscribeResultDto
        {
            Email = email,
            Status = NewsletterSubscriptionStatus.Subscribed,
            AlreadySubscribed = false,
            Message = "Thanks for subscribing!",
        };
    }
}
