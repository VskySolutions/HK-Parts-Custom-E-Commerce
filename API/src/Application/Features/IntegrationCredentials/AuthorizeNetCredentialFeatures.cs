using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ Authorize.Net ================================
// docs/authorize-net-payment-requirements.md §2 / §4. One row per environment (sandbox / production), each
// with its own keys and settings, so switching the active row never mixes them (AN-02). The Transaction Key
// and Signature Key are write-only: never returned, shown masked, kept when the field is left blank (AN-40).

/// <summary>Card brands as Authorize.Net reports them in accountType (the values the admin picks from).</summary>
public static class AuthorizeNetCardTypes
{
    public static readonly IReadOnlyList<string> All = new[] { "Visa", "MasterCard", "AmericanExpress", "Discover", "JCB", "DinersClub" };
    public static readonly IReadOnlyList<string> TransactionTypes = new[] { "AuthCapture", "AuthOnly" };
    public static readonly IReadOnlyList<string> OrderStatuses = new[] { "Pending", "Processing" };
}

public class AuthorizeNetCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApplicationLoginId { get; set; }

    /// <summary>Always null on read (write-only). See <see cref="TransactionKeyMasked"/>.</summary>
    public string? TransactionKey { get; set; }
    /// <summary>Always null on read (write-only). See <see cref="SignatureKeyMasked"/>.</summary>
    public string? SignatureKey { get; set; }
    /// <summary>"••••1234" when a key is stored, else null.</summary>
    public string? TransactionKeyMasked { get; set; }
    public string? SignatureKeyMasked { get; set; }

    public string? PublicClientKey { get; set; }
    public decimal? TransactionFeePercent { get; set; }

    public string? TransactionType { get; set; }
    public bool CustomerProfilesEnabled { get; set; }
    public bool LoggingEnabled { get; set; }
    public List<string> AcceptedCardTypes { get; set; } = new();
    public string? DisplayName { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxOrderAmount { get; set; }
    public List<Guid> AllowedCustomerGroupIds { get; set; } = new();
    public string? DefaultOrderStatus { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static AuthorizeNetCredentialDto From(AuthorizeNetCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ApplicationLoginId = e.ApplicationLoginId,
        TransactionKeyMasked = Mask(e.TransactionKey),
        SignatureKeyMasked = Mask(e.SignatureKey),
        PublicClientKey = e.PublicClientKey,
        TransactionFeePercent = e.TransactionFeePercent,
        TransactionType = e.TransactionType ?? "AuthCapture",
        CustomerProfilesEnabled = e.CustomerProfilesEnabled,
        LoggingEnabled = e.LoggingEnabled,
        AcceptedCardTypes = Split(e.AcceptedCardTypes),
        DisplayName = e.DisplayName,
        MinOrderAmount = e.MinOrderAmount,
        MaxOrderAmount = e.MaxOrderAmount,
        AllowedCustomerGroupIds = Split(e.AllowedCustomerGroupIds).Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList(),
        DefaultOrderStatus = e.DefaultOrderStatus ?? "Pending",
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };

    /// <summary>AN-40: the last four characters only, so the admin can tell which key is stored.</summary>
    public static string? Mask(string? secret)
        => string.IsNullOrEmpty(secret) ? null : "••••" + (secret.Length > 4 ? secret[^4..] : string.Empty);

    internal static List<string> Split(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}

public record ListAuthorizeNetCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListAuthorizeNetCredentialsQueryHandler
    : IRequestHandler<ListAuthorizeNetCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListAuthorizeNetCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListAuthorizeNetCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.AuthorizeNetCredentials, ct, c => c.BaseUrl);
}

public record GetAuthorizeNetCredentialQuery(Guid Id) : IRequest<AuthorizeNetCredentialDto>;

public class GetAuthorizeNetCredentialQueryHandler : IRequestHandler<GetAuthorizeNetCredentialQuery, AuthorizeNetCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetAuthorizeNetCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<AuthorizeNetCredentialDto> Handle(GetAuthorizeNetCredentialQuery request, CancellationToken ct)
        => AuthorizeNetCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.AuthorizeNetCredentials, request.Id, ct));
}

public record SaveAuthorizeNetCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ApplicationLoginId, string? TransactionKey, string? SignatureKey,
    string? PublicClientKey = null, decimal? TransactionFeePercent = null,
    string? TransactionType = null,
    bool CustomerProfilesEnabled = false,
    bool LoggingEnabled = false,
    List<string>? AcceptedCardTypes = null,
    string? DisplayName = null,
    decimal? MinOrderAmount = null,
    decimal? MaxOrderAmount = null,
    List<Guid>? AllowedCustomerGroupIds = null,
    string? DefaultOrderStatus = null) : IRequest<AuthorizeNetCredentialDto>;

public class SaveAuthorizeNetCredentialCommandValidator : AbstractValidator<SaveAuthorizeNetCredentialCommand>
{
    public SaveAuthorizeNetCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://apitest.authorize.net/xml/v1/request.api");
        // The Transaction Key travels in every request body: HTTPS only.
        RuleFor(x => x.BaseUrl)
            .Must(u => string.IsNullOrWhiteSpace(u) || u.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Authorize.Net requires HTTPS — https://apitest.authorize.net/xml/v1/request.api (sandbox) or https://api.authorize.net/xml/v1/request.api (production).");
        RuleFor(x => x.ApplicationLoginId).NotEmpty().MaximumLength(200);
        // Write-only: required when creating; on update a blank value keeps the stored key (AN-40).
        RuleFor(x => x.TransactionKey).NotEmpty().When(x => x.Id is null)
            .WithMessage("Transaction Key is required.");
        // The storefront's Accept.js flow tokenizes the card with the API Login ID + Public Client Key, so the
        // gateway cannot take a payment without it — require it rather than let checkout fail at charge time.
        RuleFor(x => x.PublicClientKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.TransactionFeePercent).InclusiveBetween(0m, 100m).When(x => x.TransactionFeePercent.HasValue);

        RuleFor(x => x.TransactionType)
            .Must(t => string.IsNullOrWhiteSpace(t) || AuthorizeNetCardTypes.TransactionTypes.Contains(t.Trim()))
            .WithMessage("Transaction type must be AuthCapture or AuthOnly.");
        RuleForEach(x => x.AcceptedCardTypes)
            .Must(c => AuthorizeNetCardTypes.All.Contains(c.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("'{PropertyValue}' is not a card type Authorize.Net reports.");
        RuleFor(x => x.DisplayName).MaximumLength(100);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MaxOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount)
            .GreaterThanOrEqualTo(x => x.MinOrderAmount!.Value)
            .When(x => x.MinOrderAmount.HasValue && x.MaxOrderAmount.HasValue)
            .WithMessage("Maximum order amount must be at least the minimum.");
        RuleFor(x => x.DefaultOrderStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || AuthorizeNetCardTypes.OrderStatuses.Contains(s.Trim()))
            .WithMessage("Default order status must be Pending or Processing.");
    }
}

public class SaveAuthorizeNetCredentialCommandHandler : IRequestHandler<SaveAuthorizeNetCredentialCommand, AuthorizeNetCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveAuthorizeNetCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<AuthorizeNetCredentialDto> Handle(SaveAuthorizeNetCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.AuthorizeNetCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ApplicationLoginId = IntegrationCredentialSupport.Norm(request.ApplicationLoginId);
        // AN-40: blank = keep what is stored (the form never receives the real value to send back).
        e.TransactionKey = IntegrationCredentialSupport.Norm(request.TransactionKey) ?? e.TransactionKey;
        e.SignatureKey = IntegrationCredentialSupport.Norm(request.SignatureKey) ?? e.SignatureKey;
        // Blank field and nothing usable stored: either no key was ever saved on this row, or it was encrypted
        // under another deployment's Data Protection key ring and reads as null. Ask for it on the field (400).
        if (string.IsNullOrWhiteSpace(e.TransactionKey))
            throw new HKPartsEcommerce.Application.Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.TransactionKey),
                    "No readable Transaction Key is stored for this credential. Enter the Transaction Key from the Authorize.Net Merchant Interface (Account → Settings → API Credentials & Keys)."),
            });
        e.PublicClientKey = IntegrationCredentialSupport.Norm(request.PublicClientKey);
        e.TransactionFeePercent = request.TransactionFeePercent;

        e.TransactionType = IntegrationCredentialSupport.Norm(request.TransactionType) ?? "AuthCapture";
        e.CustomerProfilesEnabled = request.CustomerProfilesEnabled;
        e.LoggingEnabled = request.LoggingEnabled;
        e.AcceptedCardTypes = Join(request.AcceptedCardTypes?.Select(c =>
            AuthorizeNetCardTypes.All.First(a => string.Equals(a, c.Trim(), StringComparison.OrdinalIgnoreCase))));
        e.DisplayName = IntegrationCredentialSupport.Norm(request.DisplayName);
        e.MinOrderAmount = request.MinOrderAmount;
        e.MaxOrderAmount = request.MaxOrderAmount;
        e.AllowedCustomerGroupIds = Join(request.AllowedCustomerGroupIds?.Where(g => g != Guid.Empty).Select(g => g.ToString()));
        e.DefaultOrderStatus = IntegrationCredentialSupport.Norm(request.DefaultOrderStatus) ?? "Pending";
        await _db.SaveChangesAsync(ct);
        return AuthorizeNetCredentialDto.From(e);
    }

    private static string? Join(IEnumerable<string>? values)
    {
        var list = values?.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list is { Count: > 0 } ? string.Join(",", list) : null;
    }
}

public record DeleteAuthorizeNetCredentialCommand(Guid Id) : IRequest;

public class DeleteAuthorizeNetCredentialCommandHandler : IRequestHandler<DeleteAuthorizeNetCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteAuthorizeNetCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteAuthorizeNetCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.AuthorizeNetCredentials, _db, request.Id, ct);
}

/// <summary>AN-41: authenticateTestRequest with one stored row — "connected" or the gateway's code and text.</summary>
public record TestAuthorizeNetCredentialCommand(Guid Id) : IRequest<GatewayTestResult>;

public class TestAuthorizeNetCredentialCommandHandler : IRequestHandler<TestAuthorizeNetCredentialCommand, GatewayTestResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IAuthorizeNetClient _client;
    public TestAuthorizeNetCredentialCommandHandler(IApplicationDbContext db, IAuthorizeNetClient client)
    {
        _db = db;
        _client = client;
    }

    public async Task<GatewayTestResult> Handle(TestAuthorizeNetCredentialCommand request, CancellationToken ct)
    {
        if (!await _db.AuthorizeNetCredentials.AsNoTracking().AnyAsync(x => x.Id == request.Id, ct))
            throw new NotFoundException(nameof(AuthorizeNetCredential), request.Id);
        return await _client.TestCredentialAsync(request.Id, ct);
    }
}
