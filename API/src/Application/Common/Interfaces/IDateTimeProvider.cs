namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Abstraction over the system clock to keep handlers deterministically testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
