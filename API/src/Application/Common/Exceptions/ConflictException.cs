namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>Thrown when an operation conflicts with existing state (e.g. duplicate key); HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }

    /// <param name="extensions">
    /// Extra members merged into the 409 problem body (ProblemDetails extensions) — e.g. the current
    /// values after an optimistic-concurrency miss, or the candidate list for an ambiguous lookup.
    /// </param>
    public ConflictException(string message, IReadOnlyDictionary<string, object?> extensions) : base(message)
        => Extensions = extensions;

    public IReadOnlyDictionary<string, object?>? Extensions { get; }
}
