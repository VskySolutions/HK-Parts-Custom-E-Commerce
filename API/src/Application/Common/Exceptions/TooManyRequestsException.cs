namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>A caller exceeded a rate limit (e.g. guest order lookup, U-62). Mapped to 429.</summary>
public class TooManyRequestsException : Exception
{
    public TooManyRequestsException(string message) : base(message) { }
}
