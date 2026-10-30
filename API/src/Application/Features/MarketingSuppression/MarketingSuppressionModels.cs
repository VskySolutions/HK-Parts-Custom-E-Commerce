using MarketingSuppressionEntity = HKPartsEcommerce.Domain.Entities.MarketingSuppression;

namespace HKPartsEcommerce.Application.Features.MarketingSuppression;

/// <summary>Admin list/export view of a marketing-suppressed recipient (WO-87 AC-ENT-006.5).</summary>
public class MarketingSuppressionDto
{
    public Guid Id { get; set; }
    public string RecipientEmail { get; set; } = string.Empty;
    public DateTime SuppressedOnUtc { get; set; }

    /// <summary>Origin of the suppression, e.g. "unsubscribe-link".</summary>
    public string Source { get; set; } = string.Empty;

    public static MarketingSuppressionDto From(MarketingSuppressionEntity e) => new()
    {
        Id = e.Id,
        RecipientEmail = e.RecipientEmail,
        SuppressedOnUtc = e.SuppressedOnUtc,
        Source = e.Source,
    };
}

/// <summary>
/// Outcome of processing an unsubscribe-link click. Deliberately not an exception-based flow: an invalid or
/// tampered token yields <see cref="Succeeded"/> = false so the endpoint can render a friendly page rather
/// than a JSON error (WO-87 AC-ENT-006.3).
/// </summary>
public class UnsubscribeResult
{
    public bool Succeeded { get; init; }
    public string Email { get; init; } = string.Empty;

    public static UnsubscribeResult Success(string email) => new() { Succeeded = true, Email = email };
    public static UnsubscribeResult Invalid() => new() { Succeeded = false };
}
