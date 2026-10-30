namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// Persisted log record for Error/Critical events (via the Serilog MSSQL sink) and frontend error
/// reports. Column names align with Serilog.Sinks.MSSqlServer standard + additional columns, so the
/// sink and EF Core share one table (Logging and Observability blueprint).
/// </summary>
public class ApplicationLog
{
    public long Id { get; set; }
    public string? Message { get; set; }
    public string? MessageTemplate { get; set; }
    public string Level { get; set; } = string.Empty;
    public DateTime TimeStamp { get; set; }
    public string? Exception { get; set; }
    public string? Properties { get; set; }
    public string? CorrelationId { get; set; }
    public string? Source { get; set; }   // Backend | Frontend
    public string? Route { get; set; }     // frontend error route, if applicable
}
