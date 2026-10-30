using FluentValidation.Results;

namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>Thrown by the validation pipeline behavior; surfaced as a structured HTTP 400.</summary>
public class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures have occurred.")
        => Errors = new Dictionary<string, string[]>();

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
        => Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());

    public IDictionary<string, string[]> Errors { get; }
}
