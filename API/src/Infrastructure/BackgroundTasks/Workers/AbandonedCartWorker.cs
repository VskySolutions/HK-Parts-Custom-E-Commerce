using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Detects registered-buyer carts that have been idle beyond a configurable threshold and have not yet
/// converted, and enqueues a one-time recovery email with a link back to the cart (REQ-CHK-004). A cart is
/// notified at most once (tracked by <c>Cart.AbandonmentNotifiedOnUtc</c>); marketing suppression is
/// respected; guest (session-only) carts are excluded because they carry no email.
/// </summary>
public class AbandonedCartWorker : IScheduledTask
{
    private const string TemplateKey = "cart.abandoned";
    private const int BatchSize = 200;

    public string Name => "AbandonedCartWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(60);
    public string? IntervalSettingKey => "tasks.abandoned-cart.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var settings = services.GetRequiredService<ISettingsService>();
        var enqueuer = services.GetRequiredService<IEmailEnqueuer>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var thresholdHours = await settings.GetAsync<int>("cart.abandoned.threshold-hours", cancellationToken);
        if (thresholdHours <= 0)
            thresholdHours = 24;

        var now = DateTime.UtcNow;
        var cutoff = now.AddHours(-thresholdHours);

        var baseUrl = await settings.GetAsync<string>("app.public-base-url", cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://localhost:9000";
        var cartUrl = $"{baseUrl.TrimEnd('/')}/shop/cart";

        var brandName = await settings.GetAsync<string>("app.brand-name", cancellationToken);
        if (string.IsNullOrWhiteSpace(brandName))
            brandName = "our store";

        // Candidates must be at least `threshold` old (created before the cutoff), still a live customer cart,
        // hold items, and not already have been notified. Final idle check is refined in memory below.
        var candidates = await db.Carts
            .Include(c => c.Items)
            .Where(c => !c.IsCheckedOut
                        // Customer carts, plus guest carts whose guest typed an email and opted in (guest checkout A-43 / N-06).
                        && (c.CustomerId != null || (c.ContactEmail != null && c.MarketingOptIn))
                        && c.AbandonmentNotifiedOnUtc == null
                        && c.Items.Any()
                        && c.CreatedOnUtc < cutoff)
            .OrderBy(c => c.CreatedOnUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation("{Task} ran; no abandoned carts to process.", Name);
            return;
        }

        var customerIds = candidates.Where(c => c.CustomerId != null).Select(c => c.CustomerId!.Value).Distinct().ToList();
        var customers = await db.Customers
            .Where(c => customerIds.Contains(c.Id) && c.User != null)
            .Select(c => new { c.Id, c.FirstName, c.LastName, Email = c.User!.Email })
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        var guestEmails = candidates.Where(c => c.CustomerId == null).Select(c => c.ContactEmail!).ToList();
        var guestNames = (await db.Guests.AsNoTracking()
                .Where(g => guestEmails.Contains(g.EmailNormalized))
                .Select(g => new { g.EmailNormalized, g.FirstName, g.LastName })
                .ToListAsync(cancellationToken))
            .ToDictionary(g => g.EmailNormalized, g => $"{g.FirstName} {g.LastName}".Trim(), StringComparer.OrdinalIgnoreCase);

        var emails = customers.Values
            .Select(c => c.Email)
            .Concat(guestEmails)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .ToList();
        var suppressed = (await db.MarketingSuppressions
                .Where(s => emails.Contains(s.RecipientEmail))
                .Select(s => s.RecipientEmail)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var template = await db.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == TemplateKey, cancellationToken);

        var sent = 0;
        foreach (var cart in candidates)
        {
            // Refine idle check: the latest of the cart row and its items must predate the cutoff. Item
            // mutations stamp the item (not the cart root), so the cart's own UpdatedOnUtc is insufficient.
            var lastActivity = cart.UpdatedOnUtc;
            if (cart.Items.Count > 0)
                lastActivity = new[] { lastActivity }.Concat(cart.Items.Select(i => i.UpdatedOnUtc)).Max();
            if (lastActivity >= cutoff)
                continue;

            string toEmail;
            string name;
            if (cart.CustomerId is Guid customerId)
            {
                if (!customers.TryGetValue(customerId, out var customer) || string.IsNullOrWhiteSpace(customer.Email))
                    continue;
                toEmail = customer.Email;
                name = $"{customer.FirstName} {customer.LastName}".Trim();
            }
            else
            {
                // Guest cart: only reached the candidate list with an email and a ticked opt-in.
                toEmail = cart.ContactEmail!;
                name = guestNames.TryGetValue(toEmail, out var guestName) ? guestName : string.Empty;
            }
            if (suppressed.Contains(toEmail))
                continue;

            var greeting = string.IsNullOrWhiteSpace(name) ? "there" : name;
            var subject = Substitute(template?.SubjectLine ?? "You left items in your cart", greeting, cartUrl, brandName);
            var body = Substitute(
                !string.IsNullOrWhiteSpace(template?.HtmlBody)
                    ? template!.HtmlBody
                    : $"Hi {{{{customerName}}}}, you still have items waiting in your cart at {{{{brandName}}}}. Return any time: {{{{cartUrl}}}}",
                greeting, cartUrl, brandName);

            await enqueuer.EnqueueAsync(TemplateKey, toEmail, string.IsNullOrWhiteSpace(name) ? null : name, subject, body, NotificationCategory.Marketing, isHtml: true, cancellationToken: cancellationToken);
            cart.AbandonmentNotifiedOnUtc = now;
            sent++;
        }

        if (sent > 0)
            await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("{Task} ran; sent {Sent} recovery email(s) from {Candidates} candidate cart(s).", Name, sent, candidates.Count);
    }

    private static string Substitute(string template, string customerName, string cartUrl, string brandName)
        => template
            .Replace("{{customerName}}", customerName)
            .Replace("{{cartUrl}}", cartUrl)
            .Replace("{{brandName}}", brandName);
}
