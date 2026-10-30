using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Seed;

/// <summary>Default platform settings seeded on first run. Values are later editable via the settings API.</summary>
public static class DefaultPlatformSettings
{
    public sealed record SettingSeed(string Key, string? Value, string ValueType, string Category, string Description);

    public static readonly SettingSeed[] Seeds =
    {
        new("setup.completed", "false", "bool", "Setup", "Whether the first-run setup wizard has completed."),
        new("brand.name", "HK Parts", "string", "Branding", "Display name of the storefront brand."),
        new("localization.default-language", "en", "string", "Localization", "Default UI language (ISO 639-1)."),

        // ---- Commerce mode (REQ-INQ-001) ----------------------------------------------------
        // InquiryOnly turns the storefront into a quote-request catalogue: no gateway, carrier or
        // tax provider is ever called and /api/checkout/place is refused.
        new("commerce.mode", "Standard", "string", "Commerce", "How this tenant sells (Standard | InquiryOnly)."),
        new("commerce.inquiry.show-prices", "true", "bool", "Commerce", "Show prices on the storefront in inquiry mode."),
        new("commerce.inquiry.collect-address", "true", "bool", "Commerce", "Ask for a full address on the inquiry form (off = contact details only)."),
        new("commerce.inquiry.button-label", "Request a Quote", "string", "Commerce", "Default storefront call-to-action for inquiry products."),
        new("commerce.inquiry.default-store-id", "", "string", "Commerce", "Store an inquiry is assigned to when no address is collected or routing finds none."),
        new("commerce.inquiry.notify-emails", "", "string", "Commerce", "Extra recipients for new-inquiry notifications (comma separated)."),
        new("commerce.inquiry.submit-note", "No payment is taken now — our team will review your request and get back to you.", "string", "Commerce", "Reassurance shown under the inquiry submit button."),

        new("currency.base", "USD", "string", "Currency", "Base currency (ISO 4217) all rates convert against."),
        new("currency.auto-refresh.enabled", "false", "bool", "Currency", "Whether unlocked exchange rates auto-refresh."),
        new("currency.auto-refresh.interval-hours", "24", "int", "Currency", "Hours between exchange-rate refreshes."),
        new("currency.auto-refresh.source-url", "", "string", "Currency", "Exchange-rate provider endpoint."),

        new("security.jwt.issuer", "HKPartsEcommerce", "string", "Security", "JWT issuer (iss) claim."),
        new("security.jwt.audience", "HKPartsEcommerce.Client", "string", "Security", "JWT audience (aud) claim."),
        new("security.jwt.access-token-minutes", "15", "int", "Security", "Access-token lifetime in minutes."),
        new("security.jwt.refresh-token-days", "7", "int", "Security", "Refresh-token lifetime in days."),

        new("storage.provider", "LocalFilesystem", "string", "Storage", "Active file storage provider (LocalFilesystem | AzureBlobStorage)."),
        new("storage.local.root", "wwwroot/uploads", "string", "Storage", "Root directory for local file storage."),
        new("storage.local.request-path", "/uploads", "string", "Storage", "Public URL path that serves local files."),
        new("storage.azure.container", "uploads", "string", "Storage", "Azure Blob container name."),
        new("storage.cdn.base-url", "", "string", "Storage", "Optional CDN base URL prefixed to stored file URLs."),

        new("email.dispatch.poll-seconds", "30", "int", "Email", "Interval between email queue polls."),
        new("email.retry.max-attempts", "5", "int", "Email", "Maximum delivery attempts before an email is marked failed."),
        new("email.retry.base-backoff-seconds", "60", "int", "Email", "Base seconds for exponential retry backoff."),

        new("logging.min-level", "Information", "string", "Logging", "Minimum log level written to sinks."),
        new("logging.retention-days", "90", "int", "Logging", "Days ApplicationLog rows are retained before cleanup."),
        new("logging.seq.url", "", "string", "Logging", "Optional Seq server URL for structured log shipping."),
        new("logging.seq.api-key", "", "string", "Logging", "Optional Seq API key."),
        new("logging.sentry.dsn", "", "string", "Logging", "Optional Sentry DSN for error tracking."),

        new("tasks.abandoned-cart.interval-minutes", "60", "int", "BackgroundTasks", "AbandonedCartWorker run interval."),
        new("tasks.abandoned-cart.threshold-hours", "24", "int", "BackgroundTasks", "Cart inactivity threshold before it is abandoned."),
        new("tasks.low-stock.interval-minutes", "120", "int", "BackgroundTasks", "LowStockAlertWorker run interval."),
        new("tasks.low-stock.threshold", "5", "int", "BackgroundTasks", "Stock level at/below which a low-stock alert is raised."),
        new("tasks.tracking-sync.interval-minutes", "180", "int", "BackgroundTasks", "TrackingSyncWorker run interval."),
        new("tasks.db-cleanup.cron", "0 3 * * *", "string", "BackgroundTasks", "DatabaseCleanupWorker cron schedule."),
    };

    public static IReadOnlyList<PlatformSetting> Build() =>
        Seeds.Select(s => new PlatformSetting
        {
            Key = s.Key,
            Value = s.Value,
            ValueType = s.ValueType,
            Category = s.Category,
            Description = s.Description,
        }).ToList();
}
