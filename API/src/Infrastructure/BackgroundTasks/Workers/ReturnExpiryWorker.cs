using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Rma;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Expires approved returns that were never shipped back (order-return-requirements RT-04): emails a reminder
/// 7 days before <c>LabelExpiresAtUtc</c>, then moves the return to Expired (releasing its units for a new
/// request) and tells the customer. Returns already in transit are never touched. Runs every 6 hours.
/// </summary>
public class ReturnExpiryWorker : IScheduledTask
{
    private const int BatchSize = 200;
    private static readonly TimeSpan WarnBefore = TimeSpan.FromDays(7);

    public string Name => "ReturnExpiryWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromHours(6);
    public string? IntervalSettingKey => "tasks.return-expiry.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var templates = services.GetRequiredService<IEmailTemplateSender>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);
        var now = DateTime.UtcNow;

        // ---- reminders ----
        var warnCutoff = now.Add(WarnBefore);
        var toWarn = await db.Rmas.Include(r => r.Lines)
            .Where(r => r.Status == RmaStatus.Approved && r.LabelExpiresAtUtc != null
                        && r.LabelExpiresAtUtc > now && r.LabelExpiresAtUtc <= warnCutoff
                        && r.ExpiryWarningSentOnUtc == null)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        foreach (var rma in toWarn)
        {
            var order = await RmaSupport.LoadOrderAsync(db, rma.OrderId, cancellationToken, tracked: false);
            await RmaSupport.SendCustomerAsync(templates, "return.expiring", order, rma,
                new Dictionary<string, string> { ["expiryDate"] = rma.LabelExpiresAtUtc!.Value.ToString("MMM d, yyyy") }, cancellationToken);
            rma.ExpiryWarningSentOnUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);

        // ---- expiry ----
        var toExpire = await db.Rmas.Include(r => r.Lines)
            .Where(r => r.Status == RmaStatus.Approved && r.LabelExpiresAtUtc != null && r.LabelExpiresAtUtc <= now)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        foreach (var rma in toExpire)
        {
            RmaWorkflow.Transition(db, rma, RmaStatus.Expired, RmaActor.System,
                "No shipment received before the return expired.", now);
            var order = await RmaSupport.LoadOrderAsync(db, rma.OrderId, cancellationToken, tracked: false);
            await RmaSupport.SendCustomerAsync(templates, "return.expired", order, rma, null, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("{Task} ran; {Warned} reminder(s), {Expired} return(s) expired.", Name, toWarn.Count, toExpire.Count);
    }
}
