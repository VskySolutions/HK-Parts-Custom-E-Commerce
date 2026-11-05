using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Infrastructure.Pricing;

/// <summary>One queued rebuild request. Exactly one shape is meaningful per instance.</summary>
internal sealed record PriceRuleRebuildRequest(bool All, Guid? RuleId, IReadOnlyCollection<Guid>? ProductIds);

/// <summary>In-process, unbounded rebuild queue (singleton). Consumed by <see cref="PriceRuleRebuildService"/>.</summary>
public sealed class PriceRuleRebuildQueue : IPriceRuleRebuildQueue
{
    private readonly Channel<PriceRuleRebuildRequest> _channel =
        Channel.CreateUnbounded<PriceRuleRebuildRequest>(new UnboundedChannelOptions { SingleReader = true });

    internal ChannelReader<PriceRuleRebuildRequest> Reader => _channel.Reader;

    public void EnqueueRule(Guid ruleId) => _channel.Writer.TryWrite(new PriceRuleRebuildRequest(false, ruleId, null));

    public void EnqueueAll() => _channel.Writer.TryWrite(new PriceRuleRebuildRequest(true, null, null));

    public void EnqueueForProducts(IEnumerable<Guid> productIds)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count > 0)
            _channel.Writer.TryWrite(new PriceRuleRebuildRequest(false, null, ids));
    }
}

/// <summary>
/// Single consumer of <see cref="PriceRuleRebuildQueue"/>. Waits briefly after the first request so a burst
/// (an import, a multi-step product save) coalesces, then runs each affected rule once: "all" wins over
/// everything, otherwise explicit rules and product-derived rules are rebuilt (each rule at most once).
/// Being the only consumer — plus the per-rule app lock inside the engine — keeps rebuilds single-flight.
/// </summary>
public sealed class PriceRuleRebuildService : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly PriceRuleRebuildQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<PriceRuleRebuildService> _logger;

    public PriceRuleRebuildService(
        PriceRuleRebuildQueue queue, IServiceScopeFactory scopes, ILogger<PriceRuleRebuildService> logger)
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
                if (!await reader.WaitToReadAsync(stoppingToken))
                    break;
                await Task.Delay(Debounce, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var all = false;
            var rules = new HashSet<Guid>();
            var products = new HashSet<Guid>();
            while (reader.TryRead(out var request))
            {
                all |= request.All;
                if (request.RuleId is Guid id) rules.Add(id);
                if (request.ProductIds is { } ids) products.UnionWith(ids);
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IPriceRuleEngine>();

                if (all)
                {
                    var count = await engine.RebuildAllAsync(cancellationToken: stoppingToken);
                    _logger.LogInformation("Price rules: rebuilt all {Count} rule(s).", count);
                    continue;
                }

                foreach (var ruleId in rules)
                    await engine.RebuildAsync(ruleId, stoppingToken);

                if (products.Count > 0)
                {
                    var count = await engine.RebuildForProductsAsync(products, stoppingToken);
                    _logger.LogInformation(
                        "Price rules: {Products} changed product(s) triggered {Rules} rule rebuild(s).", products.Count, count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never kill the consumer; the nightly rebuild repairs anything missed here.
                _logger.LogError(ex, "Price rule rebuild failed.");
            }
        }
    }
}
