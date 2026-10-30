using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Scalar.AspNetCore;
using Serilog;
using HKPartsEcommerce.API;
using HKPartsEcommerce.API.Logging;
using HKPartsEcommerce.API.Middleware;
using HKPartsEcommerce.Application;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Infrastructure;
using HKPartsEcommerce.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Structured logging (Serilog): console + rolling file always on; Error/Critical also persisted to
// the ApplicationLogs table via the MSSQL sink; optional Seq/Sentry via DB-backed settings (WO-6).
// Optional Seq/Sentry endpoints come from DB-backed settings, read once via a bounded best-effort
// query (a missing table on first run is ignored and picked up on the next restart) (WO-6).
var (dbSeqUrl, dbSentryDsn) = SerilogConfiguration.TryReadDbSinkSettings(builder.Configuration);
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    SerilogConfiguration.Apply(loggerConfiguration, context.Configuration, dbSeqUrl, dbSentryDsn));

// Clean Architecture layer registrations.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

// CORS for the browser SPA (WEB/). Allowed origins come from configuration
// (Cors:AllowedOrigins); falls back to the local Quasar dev server ports.
const string SpaCorsPolicy = "SpaCors";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:9000", "https://ecommerce.vskyapplications.com"];
builder.Services.AddCors(options =>
    options.AddPolicy(SpaCorsPolicy, policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

// Apply pending migrations and seed baseline data BEFORE accepting traffic.
await app.Services.InitializeDatabaseAsync();

// Resolve the local file-storage location for static serving (WO-88).
string uploadsRoot, uploadsRequestPath;
using (var scope = app.Services.CreateScope())
{
    var settings = scope.ServiceProvider.GetRequiredService<ISettingsService>();
    var configuredRoot = await settings.GetValueAsync("storage.local.root") ?? "wwwroot/uploads";
    uploadsRequestPath = (await settings.GetValueAsync("storage.local.request-path") ?? "/uploads").TrimEnd('/');
    uploadsRoot = Path.IsPathRooted(configuredRoot)
        ? configuredRoot
        : Path.Combine(app.Environment.ContentRootPath, configuredRoot);
    Directory.CreateDirectory(uploadsRoot);
}

// Correlation id first, so the request-summary log line carries it.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = uploadsRequestPath,
});
app.UseMiddleware<SetupGuardMiddleware>();

app.MapOpenApi();               // /openapi/v1.json
app.MapScalarApiReference();    // interactive docs UI

// CORS must run before auth so the browser's preflight (and cross-origin login) succeeds.
app.UseCors(SpaCorsPolicy);

// In Development the SPA calls the plain-HTTP endpoint (http://localhost:5144); forcing an HTTPS
// redirect there breaks the cross-origin preflight. Only redirect to HTTPS outside Development.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoints (unauthenticated) — see API Server blueprint.
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

app.Run();

// Exposed for integration testing.
public partial class Program;
