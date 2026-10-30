using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.ApiKeys;

/// <summary>Mints a new API key for an M2M caller and returns the plaintext once.</summary>
public record CreateApiKeyCommand(string Name, List<string> Scopes, DateTime? ExpiresAtUtc)
    : IRequest<CreatedApiKeyDto>;

public class CreateApiKeyCommandValidator : AbstractValidator<CreateApiKeyCommand>
{
    public CreateApiKeyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scopes).NotNull();
        RuleForEach(x => x.Scopes)
            .Must(Modules.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid module scope.");
        RuleFor(x => x.ExpiresAtUtc)
            .Must(d => d is null || d > DateTime.UtcNow)
            .WithMessage("Expiry must be in the future.");
    }
}

public class CreateApiKeyCommandHandler : IRequestHandler<CreateApiKeyCommand, CreatedApiKeyDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IApiKeyService _apiKeys;

    public CreateApiKeyCommandHandler(IApplicationDbContext db, IApiKeyService apiKeys)
    {
        _db = db;
        _apiKeys = apiKeys;
    }

    public async Task<CreatedApiKeyDto> Handle(CreateApiKeyCommand request, CancellationToken cancellationToken)
    {
        var material = _apiKeys.Generate();

        var entity = new ApiKey
        {
            Name = request.Name.Trim(),
            KeyHash = material.KeyHash,
            Prefix = material.Prefix,
            Scopes = (request.Scopes ?? new List<string>()).Distinct().ToList(),
            IsActive = true,
            ExpiresAtUtc = request.ExpiresAtUtc,
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreatedApiKeyDto
        {
            Id = entity.Id,
            Name = entity.Name,
            PlainTextKey = material.PlainTextKey,
            Scopes = entity.Scopes.ToList(),
            ExpiresAtUtc = entity.ExpiresAtUtc,
        };
    }
}
