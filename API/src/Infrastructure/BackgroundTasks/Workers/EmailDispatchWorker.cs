using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Email;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Delivers queued emails (Email Dispatch blueprint). Picks up <see cref="EmailStatus.Pending"/> /
/// <see cref="EmailStatus.Retry"/> rows from <see cref="EmailQueue"/> and sends each through the
/// enabled SMTP account for its category (falling back to a general account), via MailKit. Transient
/// failures retry with exponential backoff up to a configurable maximum, then are marked
/// <see cref="EmailStatus.Failed"/>. When no SMTP account is configured the emails are left Pending so
/// they send as soon as one is added — nothing is lost.
/// </summary>
public class EmailDispatchWorker : IScheduledTask
{
    private const int BatchSize = 25;
    private const int DefaultMaxAttempts = 5;
    private const int SmtpTimeoutMs = 20000;

    public string Name => "EmailDispatchWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(1);
    public string? IntervalSettingKey => "tasks.email-dispatch.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var vault = services.GetRequiredService<ICredentialVault>();
        var settings = services.GetRequiredService<ISettingsService>();
        var unsubscribeTokens = services.GetRequiredService<IUnsubscribeTokenService>();
        var publicBaseUrl = services.GetRequiredService<IConfiguration>()["Storefront:PublicBaseUrl"];
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var maxAttempts = await settings.GetAsync<int>("email.max-attempts", cancellationToken);
        if (maxAttempts <= 0)
            maxAttempts = DefaultMaxAttempts;

        var now = DateTime.UtcNow;
        var due = await db.EmailQueue
            .Where(e => (e.Status == EmailStatus.Pending || e.Status == EmailStatus.Retry)
                        && (e.NextAttemptOnUtc == null || e.NextAttemptOnUtc <= now))
            .OrderBy(e => e.CreatedOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            logger.LogInformation("{Task} ran; no emails due.", Name);
            return;
        }

        // WO-87 (AC-ENT-006.4): batch-load the marketing-suppressed recipients in this batch (mirrors
        // AbandonedCartWorker). SQL Server's IN uses the case-insensitive collation, matching the set below.
        var marketingRecipients = due
            .Where(e => e.Category == NotificationCategory.Marketing)
            .Select(e => e.RecipientEmail)
            .Distinct()
            .ToList();
        var suppressed = marketingRecipients.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await db.MarketingSuppressions
                    .Where(s => marketingRecipients.Contains(s.RecipientEmail))
                    .Select(s => s.RecipientEmail)
                    .ToListAsync(cancellationToken))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accounts = await db.SmtpAccounts
            .AsNoTracking()
            .Where(a => a.Enabled)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
        {
            // Leave the rows Pending (don't burn attempts) — they'll go out once an SMTP account exists.
            logger.LogWarning("{Task}: {Count} email(s) pending but no enabled SMTP account is configured.", Name, due.Count);
            return;
        }

        foreach (var email in due)
        {
            // WO-87 (AC-ENT-006.4): never dispatch Marketing mail to a suppressed recipient. Mark and skip
            // before burning an attempt — the row is terminal (never sent).
            if (email.Category == NotificationCategory.Marketing && suppressed.Contains(email.RecipientEmail))
            {
                email.Status = EmailStatus.Suppressed;
                email.NextAttemptOnUtc = null;
                email.ErrorMessage = null;
                continue;
            }

            var account = SelectAccount(accounts, email.Category);
            email.AttemptCount++;
            email.LastAttemptOnUtc = now;

            if (account is null)
            {
                email.ErrorMessage = "No enabled SMTP account for this email's category.";
                ScheduleRetry(email, now, maxAttempts);
                continue;
            }

            try
            {
                // WO-87 (AC-ENT-006.1): a fresh per-recipient unsubscribe link for the Marketing text/plain part.
                var unsubscribeUrl = email.Category == NotificationCategory.Marketing
                    ? MarketingEmailContent.BuildUnsubscribeUrl(publicBaseUrl, unsubscribeTokens.Generate(email.RecipientEmail))
                    : null;
                await SendAsync(email, account, vault, unsubscribeUrl, cancellationToken);
                email.Status = EmailStatus.Sent;
                email.NextAttemptOnUtc = null;
                email.ErrorMessage = null;
            }
            catch (Exception ex)
            {
                email.ErrorMessage = Truncate(ex.Message);
                ScheduleRetry(email, now, maxAttempts);
                logger.LogWarning(ex, "{Task}: send failed for {Recipient} (attempt {Attempt}).", Name, email.RecipientEmail, email.AttemptCount);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Task} ran; processed {Count} email(s).", Name, due.Count);
    }

    // Prefer the enabled account dedicated to this category, then a general (no-category) account, then any.
    private static SmtpAccount? SelectAccount(List<SmtpAccount> accounts, NotificationCategory category)
        => accounts.FirstOrDefault(a => a.Category == category)
           ?? accounts.FirstOrDefault(a => a.Category == null)
           ?? accounts.FirstOrDefault();

    private static async Task SendAsync(EmailQueue email, SmtpAccount account, ICredentialVault vault, string? unsubscribeUrl, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.FromName, account.FromEmail));
        message.To.Add(string.IsNullOrWhiteSpace(email.RecipientName)
            ? MailboxAddress.Parse(email.RecipientEmail)
            : new MailboxAddress(email.RecipientName, email.RecipientEmail));
        message.Subject = email.RenderedSubject;
        // HTML templates go out as an HTML body (with a plain-text alternative); legacy plain emails stay text.
        if (email.IsHtml)
        {
            var builder = new BodyBuilder { HtmlBody = email.RenderedBody };
            // WO-87 (AC-ENT-006.1): Marketing HTML mail also carries a text/plain alternative that contains
            // the unsubscribe link (the href in the stripped HTML anchor would otherwise be lost).
            if (unsubscribeUrl is not null)
                builder.TextBody = MarketingEmailContent.BuildTextAlternative(email.RenderedBody, unsubscribeUrl);
            message.Body = builder.ToMessageBody();
        }
        else
        {
            message.Body = new TextPart("plain") { Text = email.RenderedBody };
        }

        var secureOptions = account.EncryptionMode switch
        {
            SmtpEncryptionMode.None => SecureSocketOptions.None,
            SmtpEncryptionMode.Ssl => SecureSocketOptions.SslOnConnect,
            SmtpEncryptionMode.Tls => SecureSocketOptions.StartTls,
            SmtpEncryptionMode.StartTls => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto,
        };

        using var client = new SmtpClient { Timeout = SmtpTimeoutMs };
        await client.ConnectAsync(account.Host, account.Port, secureOptions, cancellationToken);

        // Restrict to a specific SASL mechanism when the account pins one; Auto keeps MailKit's default.
        if (account.AuthMethod != SmtpAuthMethod.Auto)
        {
            var mechanism = account.AuthMethod switch
            {
                SmtpAuthMethod.Login => "LOGIN",
                SmtpAuthMethod.Plain => "PLAIN",
                SmtpAuthMethod.CramMd5 => "CRAM-MD5",
                _ => null,
            };
            if (mechanism is not null)
            {
                client.AuthenticationMechanisms.Clear();
                client.AuthenticationMechanisms.Add(mechanism);
            }
        }

        if (!string.IsNullOrEmpty(account.Username))
        {
            var password = string.IsNullOrEmpty(account.EncryptedPassword) ? null : vault.Decrypt(account.EncryptedPassword);
            await client.AuthenticateAsync(account.Username, password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static void ScheduleRetry(EmailQueue email, DateTime now, int maxAttempts)
    {
        if (email.AttemptCount >= maxAttempts)
        {
            email.Status = EmailStatus.Failed;
            email.NextAttemptOnUtc = null;
        }
        else
        {
            email.Status = EmailStatus.Retry;
            // Exponential backoff: 2^attempt minutes, capped at 6 hours.
            var minutes = Math.Min(Math.Pow(2, email.AttemptCount), 360);
            email.NextAttemptOnUtc = now.AddMinutes(minutes);
        }
    }

    private static string Truncate(string value) => value.Length > 4000 ? value[..4000] : value;
}
