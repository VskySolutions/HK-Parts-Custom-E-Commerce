using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ Azure Blob ===================================

public class AzureBlobCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? ConnectionString { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static AzureBlobCredentialDto From(AzureBlobCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        ConnectionString = e.ConnectionString,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListAzureBlobCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListAzureBlobCredentialsQueryHandler
    : IRequestHandler<ListAzureBlobCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListAzureBlobCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListAzureBlobCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.AzureBlobCredentials, ct);
}

public record GetAzureBlobCredentialQuery(Guid Id) : IRequest<AzureBlobCredentialDto>;

public class GetAzureBlobCredentialQueryHandler : IRequestHandler<GetAzureBlobCredentialQuery, AzureBlobCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetAzureBlobCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<AzureBlobCredentialDto> Handle(GetAzureBlobCredentialQuery request, CancellationToken ct)
        => AzureBlobCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.AzureBlobCredentials, request.Id, ct));
}

public record SaveAzureBlobCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? ConnectionString) : IRequest<AzureBlobCredentialDto>;

public class SaveAzureBlobCredentialCommandValidator : AbstractValidator<SaveAzureBlobCredentialCommand>
{
    public SaveAzureBlobCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ConnectionString).NotEmpty();
    }
}

public class SaveAzureBlobCredentialCommandHandler : IRequestHandler<SaveAzureBlobCredentialCommand, AzureBlobCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveAzureBlobCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<AzureBlobCredentialDto> Handle(SaveAzureBlobCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.AzureBlobCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.ConnectionString = IntegrationCredentialSupport.Norm(request.ConnectionString);
        await _db.SaveChangesAsync(ct);
        return AzureBlobCredentialDto.From(e);
    }
}

public record DeleteAzureBlobCredentialCommand(Guid Id) : IRequest;

public class DeleteAzureBlobCredentialCommandHandler : IRequestHandler<DeleteAzureBlobCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteAzureBlobCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteAzureBlobCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.AzureBlobCredentials, _db, request.Id, ct);
}
