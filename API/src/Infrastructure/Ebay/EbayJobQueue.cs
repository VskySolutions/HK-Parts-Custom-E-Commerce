using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.Ebay;

namespace HKPartsEcommerce.Infrastructure.Ebay;

internal enum EbayJobKind { InventorySync, OutOfStock, ShipmentSync, CategoryRefresh, OrderImport }

internal sealed record EbayJob(EbayJobKind Kind, Guid? StoreId, IReadOnlyCollection<Guid>? Ids, bool Force = false, int Attempt = 0);

/// <summary>In-process eBay job queue (singleton), drained by <see cref="EbayJobService"/>.</summary>
public sealed class EbayJobQueue : IEbayJobQueue
{
    private readonly Channel<EbayJob> _channel = Channel.CreateUnbounded<EbayJob>(new UnboundedChannelOptions { SingleReader = true });

    internal ChannelReader<EbayJob> Reader => _channel.Reader;

    internal void Requeue(EbayJob job) => _channel.Writer.TryWrite(job);

    public void EnqueueInventorySync(Guid? storeId, IEnumerable<Guid>? productIds = null) =>
        _channel.Writer.TryWrite(new EbayJob(EbayJobKind.InventorySync, storeId, productIds?.Distinct().ToList()));

    public void EnqueueOutOfStock(IEnumerable<Guid> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count > 0) _channel.Writer.TryWrite(new EbayJob(EbayJobKind.OutOfStock, null, ids));
    }

    public void EnqueueShipmentSync(Guid shipmentId) =>
        _channel.Writer.TryWrite(new EbayJob(EbayJobKind.ShipmentSync, null, new[] { shipmentId }));

    public void EnqueueCategoryRefresh(Guid storeId) =>
        _channel.Writer.TryWrite(new EbayJob(EbayJobKind.CategoryRefresh, storeId, null));

    public void EnqueueOrderImport(Guid storeId, bool force = false) =>
        _channel.Writer.TryWrite(new EbayJob(EbayJobKind.OrderImport, storeId, null, force));
}

/// <summary>
/// Single consumer of the eBay queue. Waits a moment so bursts coalesce (an order import decrementing many
/// products, a bulk stock edit), then runs: out-of-stock ends first, then one inventory sync per store,
/// shipment syncs, category refreshes and order imports. Shipment syncs are retried (3 attempts, exponential
/// backoff) because a lost tracking update is invisible to the buyer. Every run is logged.
/// </summary>
public sealed class EbayJobService : BackgroundService
{
    private const int MaxShipmentAttempts = 3;
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(1);

    private readonly EbayJobQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EbayJobService> _logger;

    public EbayJobService(EbayJobQueue queue, IServiceScopeFactory scopes, ILogger<EbayJobService> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await reader.WaitToReadAsync(stoppingToken)) break;
                await Task.Delay(Debounce, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var jobs = new List<EbayJob>();
            while (reader.TryRead(out var job)) jobs.Add(job);

            await RunOutOfStockAsync(jobs, stoppingToken);
            await RunInventorySyncAsync(jobs, stoppingToken);
            foreach (var job in jobs.Where(j => j.Kind == EbayJobKind.ShipmentSync).DistinctBy(j => j.Ids!.First()))
                await RunShipmentAsync(job, stoppingToken);
            foreach (var job in jobs.Where(j => j.Kind == EbayJobKind.CategoryRefresh).DistinctBy(j => j.StoreId))
                await RunAsync("EbayCategoryRefreshJob", sp => sp.GetRequiredService<EbayCategoryRefreshService>().RefreshAsync(job.StoreId!.Value, stoppingToken), stoppingToken);
            foreach (var job in jobs.Where(j => j.Kind == EbayJobKind.OrderImport).GroupBy(j => j.StoreId))
            {
                var force = job.Any(j => j.Force);
                await RunAsync("EbayOrderImportJob", sp => sp.GetRequiredService<EbayOrderImportService>().ImportAsync(job.Key!.Value, force, stoppingToken), stoppingToken);
            }
        }
    }

    private Task RunOutOfStockAsync(List<EbayJob> jobs, CancellationToken ct)
    {
        var ids = jobs.Where(j => j.Kind == EbayJobKind.OutOfStock).SelectMany(j => j.Ids!).ToHashSet();
        return ids.Count == 0
            ? Task.CompletedTask
            : RunAsync("EbayOutOfStockJob", sp => sp.GetRequiredService<IEbayListingService>().EndOutOfStockAsync(ids, ct), ct);
    }

    private async Task RunInventorySyncAsync(List<EbayJob> jobs, CancellationToken ct)
    {
        var syncs = jobs.Where(j => j.Kind == EbayJobKind.InventorySync).ToList();
        if (syncs.Count == 0) return;

        List<Guid> stores;
        using (var scope = _scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var enabled = await db.EbayAccounts.AsNoTracking().Where(a => a.IsEnabled).Select(a => a.StoreId).ToListAsync(ct);
            stores = syncs.Any(s => s.StoreId is null)
                ? enabled
                : syncs.Select(s => s.StoreId!.Value).Distinct().Where(enabled.Contains).ToList();
        }

        foreach (var storeId in stores)
        {
            var forStore = syncs.Where(s => s.StoreId is null || s.StoreId == storeId).ToList();
            // Any request without a product filter widens the run to every active listing of the store.
            IReadOnlyCollection<Guid>? productIds = forStore.Any(s => s.Ids is null)
                ? null
                : forStore.SelectMany(s => s.Ids!).ToHashSet();
            await RunAsync("EbayInventorySyncJob", sp => sp.GetRequiredService<IEbayListingService>().SyncInventoryAsync(storeId, productIds, ct), ct);
        }
    }

    private async Task RunShipmentAsync(EbayJob job, CancellationToken ct)
    {
        var shipmentId = job.Ids!.First();
        var ok = await RunAsync("EbayShipmentSyncJob", sp => sp.GetRequiredService<EbayShipmentSyncService>().SyncAsync(shipmentId, ct), ct);
        if (ok || job.Attempt + 1 >= MaxShipmentAttempts) return;

        var delay = TimeSpan.FromSeconds(Math.Pow(4, job.Attempt + 1)); // 4s, 16s
        _logger.LogWarning("EbayShipmentSyncJob for shipment {ShipmentId} will retry in {Delay}s.", shipmentId, delay.TotalSeconds);
        _ = Task.Delay(delay, ct).ContinueWith(_ => _queue.Requeue(job with { Attempt = job.Attempt + 1 }),
            CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
    }

    /// <summary>Runs one job in its own scope; logs the outcome. Returns false on failure (never throws).</summary>
    private async Task<bool> RunAsync<T>(string name, Func<IServiceProvider, Task<T>> work, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        try
        {
            using var scope = _scopes.CreateScope();
            var result = await work(scope.ServiceProvider);
            _logger.LogInformation("{Job} finished in {Elapsed} ms: {Result}", name, (int)(DateTime.UtcNow - started).TotalMilliseconds, result);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning("{Job} skipped: {Message}", name, ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Job} failed.", name);
            return false;
        }
    }
}
