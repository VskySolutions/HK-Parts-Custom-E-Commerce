using MediatR;
using Microsoft.Extensions.Logging;

namespace HKPartsEcommerce.Application.Features.Branding;

/// <summary>Raised after the deployment's singleton branding row is created or updated.</summary>
public record TenantBrandingUpdated(Guid BrandingId, string BrandName) : INotification;

public class TenantBrandingUpdatedHandler : INotificationHandler<TenantBrandingUpdated>
{
    private readonly ILogger<TenantBrandingUpdatedHandler> _logger;

    public TenantBrandingUpdatedHandler(ILogger<TenantBrandingUpdatedHandler> logger) => _logger = logger;

    public Task Handle(TenantBrandingUpdated notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Tenant branding {BrandingId} updated (brand name '{BrandName}').",
            notification.BrandingId, notification.BrandName);
        return Task.CompletedTask;
    }
}
