namespace HKPartsEcommerce.API.Logging;

/// <summary>
/// Carrier for a browser-reported stack trace (WO-71). It is never thrown — it is constructed from the
/// posted frontend error report and handed to <c>ILogger.LogError(exception, ...)</c> so the client
/// stack lands in the standard <c>Exception</c> column of the ApplicationLogs table (via the Serilog
/// MSSQL sink) while the log Message stays a clean, summary-only value.
/// </summary>
/// <remarks>
/// Both <see cref="StackTrace"/> and <see cref="ToString"/> are overridden: the MSSQL sink renders the
/// Exception column from <c>Exception.ToString()</c>, and the framework's <c>ToString()</c> reads the
/// runtime-captured trace rather than the overridable <see cref="StackTrace"/> property — so the client
/// stack must be injected through <see cref="ToString"/> to actually populate the column.
/// </remarks>
public sealed class FrontendReportedException : Exception
{
    private readonly string? _clientStack;

    public FrontendReportedException(string? message, string? clientStack)
        : base(message)
    {
        _clientStack = clientStack;
    }

    public override string? StackTrace => _clientStack;

    public override string ToString()
        => string.IsNullOrWhiteSpace(_clientStack)
            ? base.ToString()
            : $"{GetType().FullName}: {Message}{Environment.NewLine}{_clientStack}";
}
