namespace HKPartsEcommerce.Application.Common.Exceptions;

/// <summary>Thrown when an operation conflicts with existing state (e.g. duplicate key); HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
