using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;
using MarketingSuppressionEntity = HKPartsEcommerce.Domain.Entities.MarketingSuppression;

namespace HKPartsEcommerce.Application.Features.MarketingSuppression;

/// <summary>
/// Processes a public unsubscribe-link click (WO-87). Validates the signed token, adds the recipient to the
/// Marketing Suppression List (idempotent — the table is unique on RecipientEmail), and, if the same email is
/// an active newsletter subscriber, unsubscribes it too (AC-ENT-006.3/006.5, AC-CNT-006.5). Requires no
/// authentication (AC-ENT-006.2). Never throws on a bad token — it returns an unsuccessful result so the
/// endpoint can show a friendly page. No FluentValidation validator is registered on purpose: an empty/garbage
/// token must reach the handler (and the friendly error page), not be rejected as a JSON 400.
/// </summary>
public record UnsubscribeCommand(string? Token) : IRequest<UnsubscribeResult>;

public class UnsubscribeCommandHandler : IRequestHandler<UnsubscribeCommand, UnsubscribeResult>
{
    private const string SuppressionSource = "unsubscribe-link";

    private readonly IApplicationDbContext _db;
    private readonly IUnsubscribeTokenService _tokens;
    private readonly IDateTimeProvider _clock;

    public UnsubscribeCommandHandler(IApplicationDbContext db, IUnsubscribeTokenService tokens, IDateTimeProvider clock)
    {
        _db = db;
        _tokens = tokens;
        _clock = clock;
    }

    public async Task<UnsubscribeResult> Handle(UnsubscribeCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || !_tokens.TryValidate(request.Token, out var rawEmail))
            return UnsubscribeResult.Invalid();

        var email = rawEmail.Trim();
        if (email.Length == 0)
            return UnsubscribeResult.Invalid();

        var now = _clock.UtcNow;

        // Idempotent upsert: a second click (or an already-suppressed address) is a no-op. The unique index on
        // RecipientEmail uses SQL Server's case-insensitive collation, matching this case-insensitive lookup.
        var alreadySuppressed = await _db.MarketingSuppressions
            .AnyAsync(s => s.RecipientEmail == email, cancellationToken);
        if (!alreadySuppressed)
        {
            _db.MarketingSuppressions.Add(new MarketingSuppressionEntity
            {
                RecipientEmail = email,
                SuppressedOnUtc = now,
                Source = SuppressionSource,
            });
        }

        // AC-CNT-006.5: a newsletter subscriber who uses the link is also removed from the newsletter list.
        // Newsletter rows store the email lower-cased (SubscribeCommandHandler), so match on that form.
        var normalized = email.ToLowerInvariant();
        var subscription = await _db.CMSNewsletterSubscriptions
            .FirstOrDefaultAsync(s => s.Email == normalized, cancellationToken);
        if (subscription is not null && subscription.Status != NewsletterSubscriptionStatus.Unsubscribed)
        {
            subscription.Status = NewsletterSubscriptionStatus.Unsubscribed;
            subscription.UnsubscribedOnUtc = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return UnsubscribeResult.Success(email);
    }
}
