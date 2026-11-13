namespace HKPartsEcommerce.Application.Common.Models;

/// <summary>One line of a carrier credential test: a quoted service, or the error the carrier returned for it.</summary>
public record CarrierTestLine(string Service, decimal? Price, string? Error);

/// <summary>
/// Result of an integration's Test button (USPS US-40, UPS UP-40): did authentication succeed, and what
/// did a sample domestic and (optionally) international shipment quote. The labels describe each sample so
/// the admin dialog can stay carrier-neutral.
/// </summary>
public record CarrierTestResult(
    bool TokenOk,
    string? TokenError,
    IReadOnlyList<CarrierTestLine> Domestic,
    IReadOnlyList<CarrierTestLine> International,
    string? DomesticLabel = null,
    string? InternationalLabel = null,
    DateTimeOffset? TokenValidUntil = null);
