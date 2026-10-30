using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Delivers queued webhook events (REQ-PLT-003). Each due delivery is POSTed to its subscription URL with
/// an HMAC-SHA256 <c>X-Webhook-Signature</c> header; failures retry with exponential backoff up to a
/// configurable maximum, then are marked permanently failed. One failing endpoint never blocks others.
/// </summary>
public class WebhookDispatchWorker : IScheduledTask
{
    private const int BatchSize = 50;
    private const int DefaultMaxAttempts = 5;
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

    public string Name => "WebhookDispatchWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(1);
    public string? IntervalSettingKey => "tasks.webhook-dispatch.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var httpFactory = services.GetRequiredService<IHttpClientFactory>();
        var settings = services.GetRequiredService<ISettingsService>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var maxAttempts = await settings.GetAsync<int>("webhooks.max-attempts", cancellationToken);
        if (maxAttempts <= 0)
            maxAttempts = DefaultMaxAttempts;

        var now = DateTime.UtcNow;
        var due = await db.WebhookDeliveries
            .Where(d => (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Failed)
                        && (d.NextAttemptOnUtc == null || d.NextAttemptOnUtc <= now))
            .OrderBy(d => d.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            logger.LogInformation("{Task} ran; no deliveries due.", Name);
            return;
        }

        var subscriptionIds = due.Select(d => d.SubscriptionId).Distinct().ToList();
        var subscriptions = await db.WebhookSubscriptions
            .IgnoreQueryFilters()
            .Where(s => subscriptionIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Url, s.Secret, s.IsActive, s.Deleted })
            .ToDictionaryAsync(s => s.Id, cancellationToken);

        var client = httpFactory.CreateClient("webhooks");
        client.Timeout = HttpTimeout;

        foreach (var delivery in due)
        {
            if (!subscriptions.TryGetValue(delivery.SubscriptionId, out var sub) || sub.Deleted || !sub.IsActive)
            {
                delivery.Status = WebhookDeliveryStatus.PermanentlyFailed;
                delivery.NextAttemptOnUtc = null;
                delivery.LastAttemptOnUtc = now;
                continue;
            }

            var body = BuildBody(delivery);
            var signature = Sign(body, sub.Secret);
            delivery.AttemptCount++;
            delivery.LastAttemptOnUtc = now;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, sub.Url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                request.Headers.TryAddWithoutValidation("X-Webhook-Signature", $"sha256={signature}");
                request.Headers.TryAddWithoutValidation("X-Webhook-Event", delivery.EventType);

                using var response = await client.SendAsync(request, cancellationToken);
                delivery.LastResponseStatus = (int)response.StatusCode;
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                delivery.LastResponseBody = Truncate(responseBody);

                if (response.IsSuccessStatusCode)
                {
                    delivery.Status = WebhookDeliveryStatus.Succeeded;
                    delivery.NextAttemptOnUtc = null;
                }
                else
                {
                    ScheduleRetry(delivery, now, maxAttempts);
                }
            }
            catch (Exception ex)
            {
                delivery.LastResponseStatus = null;
                delivery.LastResponseBody = Truncate(ex.Message);
                ScheduleRetry(delivery, now, maxAttempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Task} ran; processed {Count} delivery attempt(s).", Name, due.Count);
    }

    private static void ScheduleRetry(WebhookDelivery delivery, DateTime now, int maxAttempts)
    {
        if (delivery.AttemptCount >= maxAttempts)
        {
            delivery.Status = WebhookDeliveryStatus.PermanentlyFailed;
            delivery.NextAttemptOnUtc = null;
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            // Exponential backoff: 2^attempt minutes, capped at 6 hours.
            var minutes = Math.Min(Math.Pow(2, delivery.AttemptCount), 360);
            delivery.NextAttemptOnUtc = now.AddMinutes(minutes);
        }
    }

    private static string BuildBody(WebhookDelivery delivery)
    {
        using var dataDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(delivery.PayloadJson) ? "null" : delivery.PayloadJson);
        var envelope = new { eventType = delivery.EventType, occurredAt = delivery.OccurredAtUtc, data = dataDoc.RootElement };
        return JsonSerializer.Serialize(envelope);
    }

    private static string Sign(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Truncate(string value) => value.Length > 4000 ? value[..4000] : value;
}
