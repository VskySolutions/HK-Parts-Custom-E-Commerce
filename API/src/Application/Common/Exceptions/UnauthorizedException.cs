namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>Thrown when authentication fails; surfaced as HTTP 401.</summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Authentication failed.") : base(message) { }
}
