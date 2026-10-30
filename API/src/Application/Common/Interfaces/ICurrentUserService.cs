namespace HKPartsEcommerce.Application.Common.Interfaces;

/// <summary>Ambient information about the caller of the current request.</summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsInRole(string role);
    bool IsAuthenticated { get; }
    string? CorrelationId { get; }
    string? IpAddress { get; }
}
