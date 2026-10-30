using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Public Authorize.Net Accept.js config for the storefront card field: the API Login ID + Public Client Key
/// the Accept.js library needs to tokenize a card into an opaque-data nonce in the browser, and the
/// environment flag that selects the sandbox vs. live Accept.js host. Reads the single active
/// <c>Credentials_AuthorizeNet</c> row — and returns only its public values, never the Transaction/Signature keys.
/// </summary>
public record GetPublicAuthorizeNetConfigQuery : IRequest<PublicAuthorizeNetConfigDto>;

/// <summary>Public Authorize.Net config — safe to expose to the browser (API Login ID + Public Client Key are publishable).</summary>
public class PublicAuthorizeNetConfigDto
{
    /// <summary>True when both the API Login ID and Public Client Key are present, so Accept.js can tokenize.</summary>
    public bool Configured { get; set; }

    public string? ApiLoginId { get; set; }
    public string? ClientKey { get; set; }

    /// <summary>Live vs. sandbox — the storefront loads the matching Accept.js host from this.</summary>
    public bool IsProduction { get; set; }
}

public class GetPublicAuthorizeNetConfigQueryHandler : IRequestHandler<GetPublicAuthorizeNetConfigQuery, PublicAuthorizeNetConfigDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicAuthorizeNetConfigQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicAuthorizeNetConfigDto> Handle(GetPublicAuthorizeNetConfigQuery request, CancellationToken ct)
    {
        // The active row, most-recently-updated first — the same one the vault resolves at checkout.
        var credential = await _db.AuthorizeNetCredentials
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderByDescending(x => x.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return new PublicAuthorizeNetConfigDto();

        return new PublicAuthorizeNetConfigDto
        {
            ApiLoginId = credential.ApplicationLoginId,
            ClientKey = credential.PublicClientKey,
            IsProduction = credential.IsProduction,
            Configured = !string.IsNullOrWhiteSpace(credential.ApplicationLoginId)
                         && !string.IsNullOrWhiteSpace(credential.PublicClientKey),
        };
    }
}
