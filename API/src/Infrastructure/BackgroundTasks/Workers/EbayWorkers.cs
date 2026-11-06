using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;
using HKPartsEcommerce.Infrastructure.Ebay;

namespace HKPartsEcommerce.Infrastructure.BackgroundTasks.Workers;

/// <summary>Safety-net inventory/price sync every 30 minutes for every enabled eBay account.</summary>
public class EbayInventorySyncWorker : IScheduledTask
{
    public string Name => "EbayInventorySyncJob";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(30);
    public string? IntervalSettingKey => "tasks.ebay-inventory-sync.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        if (await db.EbayAccounts.AnyAsync(a => a.IsEnabled, cancellationToken))
            services.GetRequiredService<IEbayJobQueue>().EnqueueInventorySync(null);
    }
}

/// <summary>eBay order import every 15 minutes — Production accounts only (Sandbox is imported on demand).</summary>
public class EbayOrderImportWorker : IScheduledTask
{
    public string Name => "EbayOrderImportJob";
    public TimeSpan DefaultInterval => TimeSpan.FromMinutes(15);
    public string? IntervalSettingKey => "tasks.ebay-order-import.interval-minutes";
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var stores = await db.EbayAccounts.AsNoTracking()
            .Where(a => a.IsEnabled && a.Environment == EbayEnvironment.Production && a.RefreshToken != null)
            .Select(a => a.StoreId)
            .ToListAsync(cancellationToken);
        var jobs = services.GetRequiredService<IEbayJobQueue>();
        foreach (var storeId in stores)
            jobs.EnqueueOrderImport(storeId);
    }
}

/// <summary>Monthly category-tree reload: checks daily, refreshes when the last load is 30+ days old (or never).</summary>
public class EbayCategoryRefreshWorker : IScheduledTask
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    public string Name => "EbayCategoryRefreshJob";
    public TimeSpan DefaultInterval => TimeSpan.FromDays(1);
    public string? IntervalSettingKey => null;
    public string? CronSettingKey => null;

    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<IApplicationDbContext>();
        var settings = services.GetRequiredService<ISettingsService>();
        var now = services.GetRequiredService<IDateTimeProvider>().UtcNow;

        var last = await settings.GetValueAsync(EbayCategoryRefreshService.LastRefreshedSettingKey, cancellationToken);
        if (DateTime.TryParse(last, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastUtc) && now - lastUtc < MaxAge)
            return;

        // The tree is per marketplace, not per store: any connected, enabled account can fetch it.
        var storeId = await db.EbayAccounts.AsNoTracking()
            .Where(a => a.IsEnabled && a.RefreshToken != null)
            .Select(a => (Guid?)a.StoreId)
            .FirstOrDefaultAsync(cancellationToken);
        if (storeId is Guid id)
            services.GetRequiredService<IEbayJobQueue>().EnqueueCategoryRefresh(id);
    }
}
