using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Infrastructure.Persistence;

namespace HKPartsEcommerce.Infrastructure.Tax;

/// <summary>
/// Writes <see cref="TaxLog"/> rows (PHP EchoLogger, log_type avalara) on their own DbContext. A tax quote runs
/// in the middle of a checkout whose tracked changes must not be flushed early, so the log never shares the
/// caller's unit of work. Best-effort: a failed write is logged and swallowed.
/// </summary>
public class TaxLogWriter
{
    /// <summary>Request/response bodies are capped so one oversized payload can't bloat the table.</summary>
    public const int MaxBodyLength = 64_000;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TaxLogWriter> _logger;

    public TaxLogWriter(IServiceScopeFactory scopes, ILogger<TaxLogWriter> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task WriteAsync(TaxLog log, CancellationToken ct)
    {
        try
        {
            log.RequestJson = Cap(log.RequestJson);
            log.ResponseJson = Cap(log.ResponseJson);
            if (log.ErrorMessage is { Length: > 1000 })
                log.ErrorMessage = log.ErrorMessage[..1000];
            if (log.Url is { Length: > 500 })
                log.Url = log.Url[..500];
            if (log.CreatedOnUtc == default)
                log.CreatedOnUtc = DateTime.UtcNow;

            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TaxLogs.Add(log);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write the {Provider} tax log for '{Action}'.", log.Provider, log.Action);
        }
    }

    private static string? Cap(string? body) =>
        body is { Length: > MaxBodyLength } ? body[..MaxBodyLength] + "…(truncated)" : body;
}
