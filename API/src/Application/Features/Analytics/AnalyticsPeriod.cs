namespace HKPartsEcommerce.Application.Features.Analytics;

/// <summary>
/// A resolved UTC reporting window expressed as a half-open interval <c>[StartUtc, EndUtc)</c> — the end is
/// exclusive so orders on the final day are counted exactly once and never double-counted at a day boundary.
/// Shared by the analytics dashboard (WO-59) and the operational reports (WO-60).
/// </summary>
public readonly record struct AnalyticsPeriod(DateTime StartUtc, DateTime EndUtc)
{
    /// <summary>
    /// Resolves the dashboard/report period selector into a concrete UTC window. Supported
    /// <paramref name="period"/> keys (case-insensitive): <c>today</c>, <c>last7</c> (the last 7 days incl.
    /// today), <c>last30</c> (the last 30 days incl. today), and <c>custom</c> (uses <paramref name="from"/> /
    /// <paramref name="to"/> as inclusive calendar dates). Anything unrecognised — including null/empty —
    /// falls back to <c>last30</c>. All boundaries are date-aligned in UTC; <paramref name="nowUtc"/> is
    /// supplied by the caller's clock so the resolver stays pure and deterministically testable.
    /// </summary>
    public static AnalyticsPeriod Resolve(string? period, DateTime? from, DateTime? to, DateTime nowUtc)
    {
        var today = nowUtc.Date;
        var tomorrow = today.AddDays(1);

        switch ((period ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "today":
                return new AnalyticsPeriod(today, tomorrow);
            case "last7":
                return new AnalyticsPeriod(today.AddDays(-6), tomorrow);
            case "custom":
                var start = from?.Date ?? today.AddDays(-29);
                // 'to' is an inclusive calendar day, so the exclusive end is the start of the following day.
                var end = to.HasValue ? to.Value.Date.AddDays(1) : tomorrow;
                if (end <= start)
                    end = start.AddDays(1);
                return new AnalyticsPeriod(start, end);
            case "last30":
            default:
                return new AnalyticsPeriod(today.AddDays(-29), tomorrow);
        }
    }
}
