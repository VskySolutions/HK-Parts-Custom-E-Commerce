using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ FedEx ========================================

public class FedExCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? AccountNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static FedExCredentialDto From(FedExCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ApiKey = e.ApiKey, ApiSecret = e.ApiSecret, AccountNumber = e.AccountNumber,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListFedExCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListFedExCredentialsQueryHandler
    : IRequestHandler<ListFedExCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListFedExCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListFedExCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.FedExCredentials, ct, c => c.BaseUrl);
}

public record GetFedExCredentialQuery(Guid Id) : IRequest<FedExCredentialDto>;

public class GetFedExCredentialQueryHandler : IRequestHandler<GetFedExCredentialQuery, FedExCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetFedExCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<FedExCredentialDto> Handle(GetFedExCredentialQuery request, CancellationToken ct)
        => FedExCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.FedExCredentials, request.Id, ct));
}

public record SaveFedExCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ApiKey, string? ApiSecret, string? AccountNumber) : IRequest<FedExCredentialDto>;

public class SaveFedExCredentialCommandValidator : AbstractValidator<SaveFedExCredentialCommand>
{
    public SaveFedExCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://apis-sandbox.fedex.com");
        RuleFor(x => x.ApiKey).NotEmpty();
        RuleFor(x => x.ApiSecret).NotEmpty();
        // Required, not optional: FedEx rejects an account-less rate request with a 403 that names no
        // field, so letting this save blank buys a credential that authenticates and never quotes.
        RuleFor(x => x.AccountNumber).NotEmpty().MaximumLength(50)
            .WithMessage("Account number is required — FedEx rejects rate requests that name no account.");
    }
}

public class SaveFedExCredentialCommandHandler : IRequestHandler<SaveFedExCredentialCommand, FedExCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveFedExCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<FedExCredentialDto> Handle(SaveFedExCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.FedExCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ApiKey = IntegrationCredentialSupport.Norm(request.ApiKey);
        e.ApiSecret = IntegrationCredentialSupport.Norm(request.ApiSecret);
        e.AccountNumber = IntegrationCredentialSupport.Norm(request.AccountNumber);
        await _db.SaveChangesAsync(ct);
        return FedExCredentialDto.From(e);
    }
}

public record DeleteFedExCredentialCommand(Guid Id) : IRequest;

public class DeleteFedExCredentialCommandHandler : IRequestHandler<DeleteFedExCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteFedExCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteFedExCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.FedExCredentials, _db, request.Id, ct);
}

// ============================ DHL Express ==================================

public class DhlExpressCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? AccountNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static DhlExpressCredentialDto From(DhlExpressCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ApiKey = e.ApiKey, ApiSecret = e.ApiSecret, AccountNumber = e.AccountNumber,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListDhlExpressCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListDhlExpressCredentialsQueryHandler
    : IRequestHandler<ListDhlExpressCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListDhlExpressCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListDhlExpressCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.DhlExpressCredentials, ct, c => c.BaseUrl);
}

public record GetDhlExpressCredentialQuery(Guid Id) : IRequest<DhlExpressCredentialDto>;

public class GetDhlExpressCredentialQueryHandler : IRequestHandler<GetDhlExpressCredentialQuery, DhlExpressCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetDhlExpressCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<DhlExpressCredentialDto> Handle(GetDhlExpressCredentialQuery request, CancellationToken ct)
        => DhlExpressCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.DhlExpressCredentials, request.Id, ct));
}

public record SaveDhlExpressCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ApiKey, string? ApiSecret, string? AccountNumber) : IRequest<DhlExpressCredentialDto>;

public class SaveDhlExpressCredentialCommandValidator : AbstractValidator<SaveDhlExpressCredentialCommand>
{
    public SaveDhlExpressCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL including the API path, e.g. https://express.api.dhl.com/mydhlapi/test");
        RuleFor(x => x.ApiKey).NotEmpty();
        RuleFor(x => x.ApiSecret).NotEmpty();
        // Optional, unlike FedEx's: MyDHL still answers without an account, just with generic products
        // rather than the negotiated rates this field exists to get.
        RuleFor(x => x.AccountNumber).MaximumLength(50);
    }
}

public class SaveDhlExpressCredentialCommandHandler : IRequestHandler<SaveDhlExpressCredentialCommand, DhlExpressCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveDhlExpressCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<DhlExpressCredentialDto> Handle(SaveDhlExpressCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.DhlExpressCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ApiKey = IntegrationCredentialSupport.Norm(request.ApiKey);
        e.ApiSecret = IntegrationCredentialSupport.Norm(request.ApiSecret);
        e.AccountNumber = IntegrationCredentialSupport.Norm(request.AccountNumber);
        await _db.SaveChangesAsync(ct);
        return DhlExpressCredentialDto.From(e);
    }
}

public record DeleteDhlExpressCredentialCommand(Guid Id) : IRequest;

public class DeleteDhlExpressCredentialCommandHandler : IRequestHandler<DeleteDhlExpressCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteDhlExpressCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteDhlExpressCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.DhlExpressCredentials, _db, request.Id, ct);
}

// ============================ USPS =========================================

public class UspsCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ConsumerKey { get; set; }
    public string? ConsumerSecret { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static UspsCredentialDto From(UspsCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ConsumerKey = e.ConsumerKey, ConsumerSecret = e.ConsumerSecret,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListUspsCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListUspsCredentialsQueryHandler
    : IRequestHandler<ListUspsCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListUspsCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListUspsCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.UspsCredentials, ct, c => c.BaseUrl);
}

public record GetUspsCredentialQuery(Guid Id) : IRequest<UspsCredentialDto>;

public class GetUspsCredentialQueryHandler : IRequestHandler<GetUspsCredentialQuery, UspsCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetUspsCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<UspsCredentialDto> Handle(GetUspsCredentialQuery request, CancellationToken ct)
        => UspsCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.UspsCredentials, request.Id, ct));
}

public record SaveUspsCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ConsumerKey, string? ConsumerSecret) : IRequest<UspsCredentialDto>;

public class SaveUspsCredentialCommandValidator : AbstractValidator<SaveUspsCredentialCommand>
{
    public SaveUspsCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://apis-tem.usps.com");
        RuleFor(x => x.ConsumerKey).NotEmpty();
        RuleFor(x => x.ConsumerSecret).NotEmpty();
    }
}

public class SaveUspsCredentialCommandHandler : IRequestHandler<SaveUspsCredentialCommand, UspsCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveUspsCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<UspsCredentialDto> Handle(SaveUspsCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.UspsCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ConsumerKey = IntegrationCredentialSupport.Norm(request.ConsumerKey);
        e.ConsumerSecret = IntegrationCredentialSupport.Norm(request.ConsumerSecret);
        await _db.SaveChangesAsync(ct);
        return UspsCredentialDto.From(e);
    }
}

public record DeleteUspsCredentialCommand(Guid Id) : IRequest;

public class DeleteUspsCredentialCommandHandler : IRequestHandler<DeleteUspsCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteUspsCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteUspsCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.UspsCredentials, _db, request.Id, ct);
}

// ============================ UPS ==========================================

public class UpsCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? MerchantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static UpsCredentialDto From(UpsCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, MerchantId = e.MerchantId, ClientId = e.ClientId, ClientSecret = e.ClientSecret,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListUpsCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListUpsCredentialsQueryHandler
    : IRequestHandler<ListUpsCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListUpsCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListUpsCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.UpsCredentials, ct, c => c.BaseUrl);
}

public record GetUpsCredentialQuery(Guid Id) : IRequest<UpsCredentialDto>;

public class GetUpsCredentialQueryHandler : IRequestHandler<GetUpsCredentialQuery, UpsCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetUpsCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<UpsCredentialDto> Handle(GetUpsCredentialQuery request, CancellationToken ct)
        => UpsCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.UpsCredentials, request.Id, ct));
}

public record SaveUpsCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? MerchantId, string? ClientId, string? ClientSecret) : IRequest<UpsCredentialDto>;

public class SaveUpsCredentialCommandValidator : AbstractValidator<SaveUpsCredentialCommand>
{
    public SaveUpsCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://wwwcie.ups.com");
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.ClientSecret).NotEmpty();
    }
}

public class SaveUpsCredentialCommandHandler : IRequestHandler<SaveUpsCredentialCommand, UpsCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveUpsCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<UpsCredentialDto> Handle(SaveUpsCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.UpsCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.MerchantId = IntegrationCredentialSupport.Norm(request.MerchantId);
        e.ClientId = IntegrationCredentialSupport.Norm(request.ClientId);
        e.ClientSecret = IntegrationCredentialSupport.Norm(request.ClientSecret);
        await _db.SaveChangesAsync(ct);
        return UpsCredentialDto.From(e);
    }
}

public record DeleteUpsCredentialCommand(Guid Id) : IRequest;

public class DeleteUpsCredentialCommandHandler : IRequestHandler<DeleteUpsCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteUpsCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteUpsCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.UpsCredentials, _db, request.Id, ct);
}
