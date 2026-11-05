using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.PriceRules;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>
/// Nightly price-rule rebuild at 00:01 in each rule's store time zone. The scheduler is UTC-based, so this
/// wakes every 15 minutes and queues every rule whose last rebuild happened before today (store-local) once
/// that store's clock has passed 00:01 — stores in different zones roll over independently, and a missed
/// night (app down) is caught up on the next wake. Dates are applied by the resolver, so the rebuild mostly
/// drops expired rules' rows and refreshes base prices.
/// </summary>
public class PriceRuleNightlyWorker : IScheduledTask
{
    private static readonly TimeSpan RunAfter = new(0, 1, 0);

    public string Name => "PriceRuleNightlyWorker";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(15);
    public string? IntervalSettingKey => null;
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var now = services.GetRequiredService<IDateTimeProvider>().UtcNow;
        var queue = services.GetRequiredService<IPriceRuleRebuildQueue>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(Name);

        var rules = await db.PriceRules.AsNoTracking()
            .Select(r => new { r.Id, r.StoreId, r.LastAppliedAtUtc })
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
            return;

        var clock = await PriceRuleClock.LoadAsync(db, cancellationToken);
        var queued = 0;
        foreach (var rule in rules)
        {
            var localNow = clock.LocalNowFor(rule.StoreId, now);
            if (localNow.TimeOfDay < RunAfter)
                continue;

            var due = rule.LastAppliedAtUtc is not DateTime last
                      || clock.LocalNowFor(rule.StoreId, last).Date < localNow.Date;
            if (!due)
                continue;

            queue.EnqueueRule(rule.Id);
            queued++;
        }

        if (queued > 0)
            logger.LogInformation("{Task} queued {Count} nightly price-rule rebuild(s).", Name, queued);
    }
}
