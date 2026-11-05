using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Ebay;

/// <summary>Persists <see cref="EbayApiLog"/> rows. Separate from the caller's unit of work.</summary>
public interface IEbayApiLogSink
{
    Task WriteAsync(EbayApiLog entry, CancellationToken ct = default);
}

/// <summary>
/// Writes each log row through its own DI scope (its own DbContext), so a log write never flushes — or is
/// rolled back with — the caller's pending changes. Failures are swallowed: logging must never break a call.
/// </summary>
public sealed class EbayApiLogSink : IEbayApiLogSink
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<EbayApiLogSink> _logger;

    public EbayApiLogSink(IServiceScopeFactory scopes, ILogger<EbayApiLogSink> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public async Task WriteAsync(EbayApiLog entry, CancellationToken ct = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            db.EbayApiLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write the eBay API log row for {Operation}.", entry.Operation);
        }
    }
}

/// <summary>Strips secrets from logged URLs and bodies.</summary>
public static partial class EbayRedactor
{
    private const string Mask = "***REDACTED***";

    [GeneratedRegex(@"""(access_token|refresh_token|client_secret|code|token)""\s*:\s*""[^""]*""", RegexOptions.IgnoreCase)]
    private static partial Regex JsonSecret();

    [GeneratedRegex(@"(?<=[?&](code|refresh_token|access_token|client_secret)=)[^&\s]*", RegexOptions.IgnoreCase)]
    private static partial Regex FormOrQuerySecret();

    [GeneratedRegex(@"(?<=\b(?:Bearer|Basic)\s)[A-Za-z0-9\-._~+/=^#|:]+", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationValue();

    // eBay user tokens look like "v^1.1#i^1#..." — mask any that slip into a body verbatim.
    [GeneratedRegex(@"v\^1\.1#[^\s""&]+")]
    private static partial Regex EbayTokenLiteral();

    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = JsonSecret().Replace(text, m => $"\"{m.Groups[1].Value}\":\"{Mask}\"");
        // Form bodies start without "?" — prefix one so the look-behind matches the first pair too.
        var prefixed = !text.StartsWith('?') && text.Contains('=') && !text.TrimStart().StartsWith('{');
        if (prefixed) text = "?" + text;
        text = FormOrQuerySecret().Replace(text, Mask);
        if (prefixed) text = text[1..];
        text = AuthorizationValue().Replace(text, Mask);
        return EbayTokenLiteral().Replace(text, Mask);
    }
}
