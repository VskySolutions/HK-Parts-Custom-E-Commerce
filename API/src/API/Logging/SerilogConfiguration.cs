using System.Data;
using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace HKPartsEcommerce.API.Logging;

/// <summary>
/// Builds the Serilog pipeline (Logging and Observability blueprint, multi-sink ADR): console +
/// rolling file always on; Error/Critical also persisted to the ApplicationLogs table via the MSSQL
/// sink; optional Seq/Sentry sinks enabled by configuration OR DB-backed settings (WO-6).
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Applies all sinks. <paramref name="dbSeqUrl"/>/<paramref name="dbSentryDsn"/> come from DB-backed
    /// settings and take precedence over the equivalent <c>Seq:ServerUrl</c>/<c>Sentry:Dsn</c> config keys.
    /// </summary>
    public static void Apply(
        LoggerConfiguration loggerConfiguration,
        IConfiguration config,
        string? dbSeqUrl = null,
        string? dbSentryDsn = null)
    {
        loggerConfiguration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "HKPartsEcommerce.API")
            .WriteTo.Console()
            .WriteTo.File(
                path: config["Logging:File:Path"] ?? "logs/vsky-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 50_000_000,
                rollOnFileSizeLimit: true);

        // Error/Critical → ApplicationLogs table (batched). Table is EF-managed (WO-92).
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            loggerConfiguration.WriteTo.MSSqlServer(
                connectionString: connectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    AutoCreateSqlTable = false,
                    BatchPostingLimit = 50,
                    BatchPeriod = TimeSpan.FromSeconds(5),
                },
                restrictedToMinimumLevel: LogEventLevel.Error,
                columnOptions: BuildColumnOptions());
        }

        // Optional Seq sink (all structured events). DB-backed setting wins over config.
        var seqUrl = !string.IsNullOrWhiteSpace(dbSeqUrl) ? dbSeqUrl : config["Seq:ServerUrl"];
        if (!string.IsNullOrWhiteSpace(seqUrl))
            loggerConfiguration.WriteTo.Seq(seqUrl, apiKey: config["Seq:ApiKey"]);

        // Optional Sentry sink (Error/Critical). DB-backed DSN wins over config.
        var sentryDsn = !string.IsNullOrWhiteSpace(dbSentryDsn) ? dbSentryDsn : config["Sentry:Dsn"];
        if (!string.IsNullOrWhiteSpace(sentryDsn))
        {
            loggerConfiguration.WriteTo.Sentry(o =>
            {
                o.Dsn = sentryDsn;
                o.MinimumEventLevel = LogEventLevel.Error;
                o.MinimumBreadcrumbLevel = LogEventLevel.Information;
            });
        }
    }

    /// <summary>
    /// Best-effort, bounded read of the optional Seq/Sentry endpoints from DB-backed settings at
    /// bootstrap. Uses a short-timeout ADO query (never EF async-over-sync) so it cannot hang host
    /// startup; a missing table (first run) or unreachable DB simply yields no DB-backed sinks.
    /// </summary>
    public static (string? SeqUrl, string? SentryDsn) TryReadDbSinkSettings(IConfiguration config)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            return (null, null);

        string? seq = null, sentry = null;
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString) { ConnectTimeout = 5 };
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText =
                "SELECT [Key],[Value] FROM PlatformSettings WHERE [Key] IN ('logging.seq.url','logging.sentry.dsn')";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(0);
                var value = reader.IsDBNull(1) ? null : reader.GetString(1);
                if (key == "logging.seq.url") seq = value;
                else if (key == "logging.sentry.dsn") sentry = value;
            }
        }
        catch
        {
            // Table not present (first run) or DB unreachable — optional sinks are skipped this run.
        }

        return (string.IsNullOrWhiteSpace(seq) ? null : seq, string.IsNullOrWhiteSpace(sentry) ? null : sentry);
    }

    private static ColumnOptions BuildColumnOptions()
    {
        var options = new ColumnOptions();
        options.TimeStamp.ConvertToUtc = true;

        // Extra columns populated from log-event properties of the same name (e.g. CorrelationId
        // pushed by CorrelationIdMiddleware; Source/Route set by frontend error ingestion).
        options.AdditionalColumns = new List<SqlColumn>
        {
            new("CorrelationId", SqlDbType.NVarChar, allowNull: true, dataLength: 64),
            new("Source", SqlDbType.NVarChar, allowNull: true, dataLength: 50),
            new("Route", SqlDbType.NVarChar, allowNull: true, dataLength: 500),
        };
        return options;
    }
}
