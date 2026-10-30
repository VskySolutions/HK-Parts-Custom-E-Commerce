using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Payments;

/// <summary>
/// Public Square Web Payments SDK config for the storefront card field: the Application Id + Location Id the
/// SDK needs to render and tokenize a card, and the environment flag that selects the sandbox vs. live SDK.
/// Reads the single active <c>Credentials_Square</c> row — and returns only its public values, never the
/// access token or application secret.
/// </summary>
public record GetPublicSquareConfigQuery : IRequest<PublicSquareConfigDto>;

/// <summary>Public Square config — safe to expose to the browser (Application Id / Location Id are publishable).</summary>
public class PublicSquareConfigDto
{
    /// <summary>True when both the Application Id and Location Id are present, so the SDK can be initialised.</summary>
    public bool Configured { get; set; }

    public string? ApplicationId { get; set; }
    public string? LocationId { get; set; }

    /// <summary>Live vs. sandbox — the storefront loads the matching Web Payments SDK host from this.</summary>
    public bool IsProduction { get; set; }
}

public class GetPublicSquareConfigQueryHandler : IRequestHandler<GetPublicSquareConfigQuery, PublicSquareConfigDto>
{
    private readonly IApplicationDbContext _db;

    public GetPublicSquareConfigQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PublicSquareConfigDto> Handle(GetPublicSquareConfigQuery request, CancellationToken ct)
    {
        // The active row, most-recently-updated first — the same one the vault resolves at checkout.
        var credential = await _db.SquareCredentials
            .AsNoTracking()
            .Where(x => x.Active)
            .OrderByDescending(x => x.UpdatedOnUtc)
            .FirstOrDefaultAsync(ct);

        if (credential is null)
            return new PublicSquareConfigDto();

        return new PublicSquareConfigDto
        {
            ApplicationId = credential.ApplicationId,
            LocationId = credential.LocationId,
            IsProduction = credential.IsProduction,
            Configured = !string.IsNullOrWhiteSpace(credential.ApplicationId)
                         && !string.IsNullOrWhiteSpace(credential.LocationId),
        };
    }
}
