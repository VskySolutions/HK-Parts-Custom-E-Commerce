namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>Thrown when the caller lacks permission for an operation; surfaced as HTTP 403.</summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException() : base("Access to the requested resource is forbidden.") { }
    public ForbiddenAccessException(string message) : base(message) { }
}
