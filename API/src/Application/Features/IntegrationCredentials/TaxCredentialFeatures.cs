using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ TaxJar =======================================

public class TaxJarCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? SecretKey { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static TaxJarCredentialDto From(TaxJarCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, SecretKey = e.SecretKey,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListTaxJarCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListTaxJarCredentialsQueryHandler
    : IRequestHandler<ListTaxJarCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListTaxJarCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListTaxJarCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.TaxJarCredentials, ct, c => c.BaseUrl);
}

public record GetTaxJarCredentialQuery(Guid Id) : IRequest<TaxJarCredentialDto>;

public class GetTaxJarCredentialQueryHandler : IRequestHandler<GetTaxJarCredentialQuery, TaxJarCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetTaxJarCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<TaxJarCredentialDto> Handle(GetTaxJarCredentialQuery request, CancellationToken ct)
        => TaxJarCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.TaxJarCredentials, request.Id, ct));
}

public record SaveTaxJarCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? SecretKey) : IRequest<TaxJarCredentialDto>;

public class SaveTaxJarCredentialCommandValidator : AbstractValidator<SaveTaxJarCredentialCommand>
{
    public SaveTaxJarCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://api.sandbox.taxjar.com");
        RuleFor(x => x.SecretKey).NotEmpty();
    }
}

public class SaveTaxJarCredentialCommandHandler : IRequestHandler<SaveTaxJarCredentialCommand, TaxJarCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveTaxJarCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<TaxJarCredentialDto> Handle(SaveTaxJarCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.TaxJarCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.SecretKey = IntegrationCredentialSupport.Norm(request.SecretKey);
        await _db.SaveChangesAsync(ct);
        return TaxJarCredentialDto.From(e);
    }
}

public record DeleteTaxJarCredentialCommand(Guid Id) : IRequest;

public class DeleteTaxJarCredentialCommandHandler : IRequestHandler<DeleteTaxJarCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteTaxJarCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteTaxJarCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.TaxJarCredentials, _db, request.Id, ct);
}

// ============================ Stripe Tax ===================================

public class StripeTaxCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? PublishableKey { get; set; }
    public string? SecretKey { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static StripeTaxCredentialDto From(StripeTaxCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, PublishableKey = e.PublishableKey, SecretKey = e.SecretKey,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListStripeTaxCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListStripeTaxCredentialsQueryHandler
    : IRequestHandler<ListStripeTaxCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListStripeTaxCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListStripeTaxCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.StripeTaxCredentials, ct, c => c.BaseUrl);
}

public record GetStripeTaxCredentialQuery(Guid Id) : IRequest<StripeTaxCredentialDto>;

public class GetStripeTaxCredentialQueryHandler : IRequestHandler<GetStripeTaxCredentialQuery, StripeTaxCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetStripeTaxCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<StripeTaxCredentialDto> Handle(GetStripeTaxCredentialQuery request, CancellationToken ct)
        => StripeTaxCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.StripeTaxCredentials, request.Id, ct));
}

public record SaveStripeTaxCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? PublishableKey, string? SecretKey) : IRequest<StripeTaxCredentialDto>;

public class SaveStripeTaxCredentialCommandValidator : AbstractValidator<SaveStripeTaxCredentialCommand>
{
    public SaveStripeTaxCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://api.stripe.com");
        RuleFor(x => x.SecretKey).NotEmpty();
    }
}

public class SaveStripeTaxCredentialCommandHandler : IRequestHandler<SaveStripeTaxCredentialCommand, StripeTaxCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveStripeTaxCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<StripeTaxCredentialDto> Handle(SaveStripeTaxCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.StripeTaxCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.PublishableKey = IntegrationCredentialSupport.Norm(request.PublishableKey);
        e.SecretKey = IntegrationCredentialSupport.Norm(request.SecretKey);
        await _db.SaveChangesAsync(ct);
        return StripeTaxCredentialDto.From(e);
    }
}

public record DeleteStripeTaxCredentialCommand(Guid Id) : IRequest;

public class DeleteStripeTaxCredentialCommandHandler : IRequestHandler<DeleteStripeTaxCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteStripeTaxCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteStripeTaxCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.StripeTaxCredentials, _db, request.Id, ct);
}
