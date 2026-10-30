using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ApiKeys;

/// <summary>Revokes an API key (deactivates it); subsequent authentication attempts are rejected with 401.</summary>
public record RevokeApiKeyCommand(Guid Id) : IRequest;

public class RevokeApiKeyCommandHandler : IRequestHandler<RevokeApiKeyCommand>
{
    private readonly IApplicationDbContext _db;

    public RevokeApiKeyCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(RevokeApiKeyCommand request, CancellationToken cancellationToken)
    {
        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ApiKey), request.Id);

        key.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
    }
}
