namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>One business error reported by an external marketplace (e.g. eBay).</summary>
public sealed record ExternalError(string? Code, string Message, string? Field = null);

/// <summary>
/// The external service rejected the request for a business reason (HTTP 422). The body carries
/// <c>errors: [{ code, message, field? }]</c> — error-severity messages only; warnings are dropped.
/// </summary>
public class ExternalBusinessException : Exception
{
    public ExternalBusinessException(string message, IReadOnlyList<ExternalError> errors) : base(message)
        => Errors = errors;

    public IReadOnlyList<ExternalError> Errors { get; }
}

/// <summary>A required external integration is unavailable (not connected, disabled, token revoked) — HTTP 503.</summary>
public class ServiceUnavailableException : Exception
{
    public ServiceUnavailableException(string message) : base(message) { }
}
