using Microsoft.Extensions.Logging;

namespace HKPartsEcommerce.Infrastructure.Shipping.Carriers;

/// <summary>
/// Records why a carrier's HTTP call failed.
/// <para>
/// Every adapter deliberately swallows a failed rate call and returns no options, so that one broken
/// integration can never block checkout (AC-SHP-001.3). The cost of that design is diagnostic: a
/// misconfigured carrier becomes indistinguishable from one that genuinely has nothing to offer — both
/// surface as "returned no rates" — and the status code and error body, the only things that say *why*,
/// are discarded at the point they are known. A wrong base URL reads as 404, a missing account number as
/// 403, an incomplete body as 400, and none of that reaches the log.
/// </para>
/// <para>
/// This writes it down once, verbatim, so the next failure is read rather than reconstructed.
/// </para>
/// </summary>
internal static class CarrierHttpDiagnostics
{
    // Carrier error bodies are small JSON documents, but nothing guarantees that — an HTML error page from
    // a proxy would otherwise land in the log whole.
    private const int MaxBodyChars = 1000;

    /// <summary>
    /// Logs a non-success carrier response with its status and body. <paramref name="operation"/> names the
    /// call ("rate", "OAuth token"), since a carrier that authenticates and then fails to rate is a very
    /// different problem from one that never authenticated.
    /// </summary>
    public static async Task LogFailedResponseAsync(
        ILogger logger,
        string carrier,
        string operation,
        HttpResponseMessage response,
        CancellationToken ct)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            // The status code is the point; reading the body must never turn a logged failure into a
            // thrown one, least of all inside the catch-all these adapters rely on.
            body = $"<body unreadable: {ex.GetType().Name}>";
        }

        if (body.Length > MaxBodyChars)
            body = string.Concat(body.AsSpan(0, MaxBodyChars), "…");

        logger.LogWarning(
            "Shipping carrier {Carrier} {Operation} call failed: HTTP {StatusCode} {ReasonPhrase}. Response: {Body}",
            carrier, operation, (int)response.StatusCode, response.ReasonPhrase, body);
    }
}
