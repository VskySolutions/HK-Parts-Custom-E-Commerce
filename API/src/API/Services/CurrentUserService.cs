using System.Security.Claims;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.API.Services;

/// <summary>Resolves the current caller's identity from the authenticated <see cref="HttpContext"/>.</summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue("sub");
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email) ?? Principal?.FindFirstValue("email");

    public IReadOnlyCollection<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? Array.Empty<string>();

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? CorrelationId => _accessor.HttpContext?.Items["CorrelationId"] as string;

    public string? IpAddress => _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
