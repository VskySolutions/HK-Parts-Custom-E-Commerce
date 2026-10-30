using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ Twilio =======================================

public class TwilioCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    public string? WhatsAppNumber { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static TwilioCredentialDto From(TwilioCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        AccountSid = e.AccountSid, AuthToken = e.AuthToken, WhatsAppNumber = e.WhatsAppNumber,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListTwilioCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListTwilioCredentialsQueryHandler
    : IRequestHandler<ListTwilioCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListTwilioCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListTwilioCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.TwilioCredentials, ct);
}

public record GetTwilioCredentialQuery(Guid Id) : IRequest<TwilioCredentialDto>;

public class GetTwilioCredentialQueryHandler : IRequestHandler<GetTwilioCredentialQuery, TwilioCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetTwilioCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<TwilioCredentialDto> Handle(GetTwilioCredentialQuery request, CancellationToken ct)
        => TwilioCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.TwilioCredentials, request.Id, ct));
}

public record SaveTwilioCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? AccountSid, string? AuthToken, string? WhatsAppNumber) : IRequest<TwilioCredentialDto>;

public class SaveTwilioCredentialCommandValidator : AbstractValidator<SaveTwilioCredentialCommand>
{
    public SaveTwilioCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AccountSid).NotEmpty();
        RuleFor(x => x.AuthToken).NotEmpty();
        RuleFor(x => x.WhatsAppNumber).MaximumLength(40);
    }
}

public class SaveTwilioCredentialCommandHandler : IRequestHandler<SaveTwilioCredentialCommand, TwilioCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveTwilioCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<TwilioCredentialDto> Handle(SaveTwilioCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.TwilioCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.AccountSid = IntegrationCredentialSupport.Norm(request.AccountSid);
        e.AuthToken = IntegrationCredentialSupport.Norm(request.AuthToken);
        e.WhatsAppNumber = IntegrationCredentialSupport.Norm(request.WhatsAppNumber);
        await _db.SaveChangesAsync(ct);
        return TwilioCredentialDto.From(e);
    }
}

public record DeleteTwilioCredentialCommand(Guid Id) : IRequest;

public class DeleteTwilioCredentialCommandHandler : IRequestHandler<DeleteTwilioCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteTwilioCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteTwilioCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.TwilioCredentials, _db, request.Id, ct);
}
