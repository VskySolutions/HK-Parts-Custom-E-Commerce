using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

/// <summary>
/// Quick active/inactive toggle for a single credential row, dispatched by integration slug so one
/// endpoint serves every <c>Credentials_*</c> table. Activating a row deactivates the integration's other
/// rows (single active row per integration).
/// </summary>
public record SetIntegrationCredentialActiveCommand(string Provider, Guid Id, bool Active) : IRequest;

public class SetIntegrationCredentialActiveCommandHandler : IRequestHandler<SetIntegrationCredentialActiveCommand>
{
    private readonly IApplicationDbContext _db;

    public SetIntegrationCredentialActiveCommandHandler(IApplicationDbContext db) => _db = db;

    public Task Handle(SetIntegrationCredentialActiveCommand request, CancellationToken ct)
    {
        var id = request.Id;
        var active = request.Active;

        return request.Provider.ToLowerInvariant() switch
        {
            "stripe"       => IntegrationCredentialSupport.SetActiveAsync(_db.StripeCredentials, _db, id, active, ct),
            "paypal"       => IntegrationCredentialSupport.SetActiveAsync(_db.PayPalCredentials, _db, id, active, ct),
            "razorpay"     => IntegrationCredentialSupport.SetActiveAsync(_db.RazorpayCredentials, _db, id, active, ct),
            "square"       => IntegrationCredentialSupport.SetActiveAsync(_db.SquareCredentials, _db, id, active, ct),
            "authorizenet" => IntegrationCredentialSupport.SetActiveAsync(_db.AuthorizeNetCredentials, _db, id, active, ct),
            "taxjar"       => IntegrationCredentialSupport.SetActiveAsync(_db.TaxJarCredentials, _db, id, active, ct),
            "stripe-tax"   => IntegrationCredentialSupport.SetActiveAsync(_db.StripeTaxCredentials, _db, id, active, ct),
            "fedex"        => IntegrationCredentialSupport.SetActiveAsync(_db.FedExCredentials, _db, id, active, ct),
            "dhl"          => IntegrationCredentialSupport.SetActiveAsync(_db.DhlExpressCredentials, _db, id, active, ct),
            "usps"         => IntegrationCredentialSupport.SetActiveAsync(_db.UspsCredentials, _db, id, active, ct),
            "ups"          => IntegrationCredentialSupport.SetActiveAsync(_db.UpsCredentials, _db, id, active, ct),
            "twilio"       => IntegrationCredentialSupport.SetActiveAsync(_db.TwilioCredentials, _db, id, active, ct),
            "azure-blob"   => IntegrationCredentialSupport.SetActiveAsync(_db.AzureBlobCredentials, _db, id, active, ct),
            _ => throw new NotFoundException($"Unknown integration '{request.Provider}'."),
        };
    }
}
