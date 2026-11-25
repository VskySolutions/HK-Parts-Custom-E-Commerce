using Microsoft.EntityFrameworkCore;
using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.IntegrationCredentials;

// ============================ Stripe =======================================

public class StripeCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? PublishableKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ReturnUrl { get; set; }
    public decimal? TransactionFeePercent { get; set; }

    // PHP parity: the legacy Stripe method settings.
    public string TransactionType { get; set; } = "AuthCapture";
    public bool CustomerProfilesEnabled { get; set; }
    public List<string> AcceptedCardTypes { get; set; } = new();
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxOrderAmount { get; set; }
    public List<Guid> AllowedCustomerGroupIds { get; set; } = new();
    public string DefaultOrderStatus { get; set; } = "Pending";
    public int SortOrder { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static StripeCredentialDto From(StripeCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        PublishableKey = e.PublishableKey, SecretKey = e.SecretKey, ReturnUrl = e.ReturnUrl,
        TransactionFeePercent = e.TransactionFeePercent,
        TransactionType = e.TransactionType ?? "AuthCapture",
        CustomerProfilesEnabled = e.CustomerProfilesEnabled,
        AcceptedCardTypes = StripeCardTypes.Split(e.AcceptedCardTypes),
        MinOrderAmount = e.MinOrderAmount,
        MaxOrderAmount = e.MaxOrderAmount,
        AllowedCustomerGroupIds = StripeCardTypes.Split(e.AllowedCustomerGroupIds)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
        DefaultOrderStatus = e.DefaultOrderStatus ?? "Pending",
        SortOrder = e.SortOrder,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

/// <summary>
/// The card brands the Stripe method can be limited to (PHP parity "Accepted Card Types"), and how Stripe names
/// each one on a charge (<c>payment_method_details.card.brand</c>). Plus the shared list helpers.
/// </summary>
public static class StripeCardTypes
{
    public static readonly IReadOnlyDictionary<string, string> StripeBrandByType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Visa"] = "visa",
        ["MasterCard"] = "mastercard",
        ["AmericanExpress"] = "amex",
        ["Discover"] = "discover",
        ["JCB"] = "jcb",
        ["DinersClub"] = "diners",
        ["UnionPay"] = "unionpay",
    };

    public static readonly string[] TransactionTypes = { "AuthCapture", "AuthOnly" };
    public static readonly string[] OrderStatuses = { "Pending", "Processing" };

    public static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? new List<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    public static string? Join(IEnumerable<string>? values)
    {
        var list = values?.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct().ToList();
        return list is { Count: > 0 } ? string.Join(",", list) : null;
    }
}

public record ListStripeCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListStripeCredentialsQueryHandler
    : IRequestHandler<ListStripeCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListStripeCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListStripeCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.StripeCredentials, ct);
}

public record GetStripeCredentialQuery(Guid Id) : IRequest<StripeCredentialDto>;

public class GetStripeCredentialQueryHandler : IRequestHandler<GetStripeCredentialQuery, StripeCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetStripeCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<StripeCredentialDto> Handle(GetStripeCredentialQuery request, CancellationToken ct)
        => StripeCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.StripeCredentials, request.Id, ct));
}

public record SaveStripeCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? PublishableKey, string? SecretKey, string? ReturnUrl,
    decimal? TransactionFeePercent = null,
    string? TransactionType = null,
    bool CustomerProfilesEnabled = false,
    List<string>? AcceptedCardTypes = null,
    decimal? MinOrderAmount = null,
    decimal? MaxOrderAmount = null,
    List<Guid>? AllowedCustomerGroupIds = null,
    string? DefaultOrderStatus = null,
    int? SortOrder = null) : IRequest<StripeCredentialDto>;

public class SaveStripeCredentialCommandValidator : AbstractValidator<SaveStripeCredentialCommand>
{
    public SaveStripeCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SecretKey).NotEmpty();
        RuleFor(x => x.TransactionFeePercent).InclusiveBetween(0m, 100m).When(x => x.TransactionFeePercent.HasValue);
        RuleFor(x => x.TransactionType)
            .Must(t => string.IsNullOrWhiteSpace(t) || StripeCardTypes.TransactionTypes.Contains(t.Trim()))
            .WithMessage("Transaction type must be AuthCapture or AuthOnly.");
        RuleForEach(x => x.AcceptedCardTypes)
            .Must(c => StripeCardTypes.StripeBrandByType.ContainsKey(c.Trim()))
            .WithMessage("'{PropertyValue}' is not a card type Stripe reports.");
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MaxOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount)
            .GreaterThanOrEqualTo(x => x.MinOrderAmount!.Value)
            .When(x => x.MinOrderAmount.HasValue && x.MaxOrderAmount.HasValue)
            .WithMessage("Maximum order amount must be at least the minimum.");
        RuleFor(x => x.DefaultOrderStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || StripeCardTypes.OrderStatuses.Contains(s.Trim()))
            .WithMessage("Default order status must be Pending or Processing.");
        RuleFor(x => x.SortOrder).InclusiveBetween(-1000, 1000).When(x => x.SortOrder.HasValue);
    }
}

public class SaveStripeCredentialCommandHandler : IRequestHandler<SaveStripeCredentialCommand, StripeCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveStripeCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<StripeCredentialDto> Handle(SaveStripeCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.StripeCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.PublishableKey = IntegrationCredentialSupport.Norm(request.PublishableKey);
        e.SecretKey = IntegrationCredentialSupport.Norm(request.SecretKey);
        e.ReturnUrl = IntegrationCredentialSupport.Norm(request.ReturnUrl);
        e.TransactionFeePercent = request.TransactionFeePercent;
        e.TransactionType = IntegrationCredentialSupport.Norm(request.TransactionType) ?? "AuthCapture";
        e.CustomerProfilesEnabled = request.CustomerProfilesEnabled;
        e.AcceptedCardTypes = StripeCardTypes.Join(request.AcceptedCardTypes?.Select(c =>
            StripeCardTypes.StripeBrandByType.Keys.First(k => string.Equals(k, c.Trim(), StringComparison.OrdinalIgnoreCase))));
        e.MinOrderAmount = request.MinOrderAmount;
        e.MaxOrderAmount = request.MaxOrderAmount;
        e.AllowedCustomerGroupIds = StripeCardTypes.Join(request.AllowedCustomerGroupIds?.Where(g => g != Guid.Empty).Select(g => g.ToString()));
        e.DefaultOrderStatus = IntegrationCredentialSupport.Norm(request.DefaultOrderStatus) ?? "Pending";
        e.SortOrder = request.SortOrder ?? 0;
        await _db.SaveChangesAsync(ct);
        return StripeCredentialDto.From(e);
    }
}

public record DeleteStripeCredentialCommand(Guid Id) : IRequest;

public class DeleteStripeCredentialCommandHandler : IRequestHandler<DeleteStripeCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteStripeCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteStripeCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.StripeCredentials, _db, request.Id, ct);
}

// ============================ PayPal =======================================

public class PayPalCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ClientId { get; set; }
    public string? SecretKey { get; set; }
    public string? ReturnUrl { get; set; }
    public decimal? TransactionFeePercent { get; set; }

    // PHP parity ("PayPal Express"): classic API credentials and method settings.
    public string? ApiUsername { get; set; }
    /// <summary>Write-only: the last four characters only, never the value.</summary>
    public string? ApiPasswordMasked { get; set; }
    public string? ApiSignatureMasked { get; set; }
    public string? BrandName { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxOrderAmount { get; set; }
    public List<Guid> AllowedCustomerGroupIds { get; set; } = new();
    public string DefaultOrderStatus { get; set; } = "Pending";
    public int SortOrder { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static PayPalCredentialDto From(PayPalCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ClientId = e.ClientId, SecretKey = e.SecretKey, ReturnUrl = e.ReturnUrl,
        TransactionFeePercent = e.TransactionFeePercent,
        ApiUsername = e.ApiUsername,
        ApiPasswordMasked = AuthorizeNetCredentialDto.Mask(e.ApiPassword),
        ApiSignatureMasked = AuthorizeNetCredentialDto.Mask(e.ApiSignature),
        BrandName = e.BrandName,
        MinOrderAmount = e.MinOrderAmount,
        MaxOrderAmount = e.MaxOrderAmount,
        AllowedCustomerGroupIds = StripeCardTypes.Split(e.AllowedCustomerGroupIds)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
        DefaultOrderStatus = e.DefaultOrderStatus ?? "Pending",
        SortOrder = e.SortOrder,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListPayPalCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListPayPalCredentialsQueryHandler
    : IRequestHandler<ListPayPalCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListPayPalCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListPayPalCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.PayPalCredentials, ct, c => c.BaseUrl);
}

public record GetPayPalCredentialQuery(Guid Id) : IRequest<PayPalCredentialDto>;

public class GetPayPalCredentialQueryHandler : IRequestHandler<GetPayPalCredentialQuery, PayPalCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetPayPalCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<PayPalCredentialDto> Handle(GetPayPalCredentialQuery request, CancellationToken ct)
        => PayPalCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.PayPalCredentials, request.Id, ct));
}

public record SavePayPalCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ClientId, string? SecretKey, string? ReturnUrl = null,
    decimal? TransactionFeePercent = null,
    string? ApiUsername = null,
    string? ApiPassword = null,
    string? ApiSignature = null,
    string? BrandName = null,
    decimal? MinOrderAmount = null,
    decimal? MaxOrderAmount = null,
    List<Guid>? AllowedCustomerGroupIds = null,
    string? DefaultOrderStatus = null,
    int? SortOrder = null) : IRequest<PayPalCredentialDto>;

public class SavePayPalCredentialCommandValidator : AbstractValidator<SavePayPalCredentialCommand>
{
    public SavePayPalCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // Optional: empty = PayPal's endpoint for the Sandbox / Production switch ("Test Mode").
        RuleFor(x => x.BaseUrl).MaximumLength(500)
            .Must(u => string.IsNullOrWhiteSpace(u) || IntegrationCredentialSupport.BeAnAbsoluteHttpUrl(u))
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://api-m.sandbox.paypal.com");
        // REST: both or neither. Which set is complete (REST or classic) is checked on save, with what is stored.
        RuleFor(x => x.SecretKey).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.ClientId))
            .WithMessage("Enter the Secret for this Client ID.");
        RuleFor(x => x.ClientId).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.SecretKey))
            .WithMessage("Enter the Client ID for this Secret.");
        RuleFor(x => x.ApiUsername).MaximumLength(200);
        RuleFor(x => x.BrandName).MaximumLength(127);
        RuleFor(x => x.TransactionFeePercent).InclusiveBetween(0m, 100m).When(x => x.TransactionFeePercent.HasValue);
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MaxOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount)
            .GreaterThanOrEqualTo(x => x.MinOrderAmount!.Value)
            .When(x => x.MinOrderAmount.HasValue && x.MaxOrderAmount.HasValue)
            .WithMessage("Maximum order amount must be at least the minimum.");
        RuleFor(x => x.DefaultOrderStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || StripeCardTypes.OrderStatuses.Contains(s.Trim()))
            .WithMessage("Default order status must be Pending or Processing.");
        RuleFor(x => x.SortOrder).InclusiveBetween(-1000, 1000).When(x => x.SortOrder.HasValue);
    }
}

public class SavePayPalCredentialCommandHandler : IRequestHandler<SavePayPalCredentialCommand, PayPalCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SavePayPalCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<PayPalCredentialDto> Handle(SavePayPalCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.PayPalCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ClientId = IntegrationCredentialSupport.Norm(request.ClientId);
        e.SecretKey = IntegrationCredentialSupport.Norm(request.SecretKey);
        e.ReturnUrl = IntegrationCredentialSupport.Norm(request.ReturnUrl);
        e.TransactionFeePercent = request.TransactionFeePercent;

        // Classic API: the password and signature are write-only, so blank keeps what is stored.
        e.ApiUsername = IntegrationCredentialSupport.Norm(request.ApiUsername);
        e.ApiPassword = IntegrationCredentialSupport.Norm(request.ApiPassword) ?? (e.ApiUsername is null ? null : e.ApiPassword);
        e.ApiSignature = IntegrationCredentialSupport.Norm(request.ApiSignature) ?? (e.ApiUsername is null ? null : e.ApiSignature);
        e.BrandName = IntegrationCredentialSupport.Norm(request.BrandName);
        e.MinOrderAmount = request.MinOrderAmount;
        e.MaxOrderAmount = request.MaxOrderAmount;
        e.AllowedCustomerGroupIds = StripeCardTypes.Join(request.AllowedCustomerGroupIds?.Where(g => g != Guid.Empty).Select(g => g.ToString()));
        e.DefaultOrderStatus = IntegrationCredentialSupport.Norm(request.DefaultOrderStatus) ?? "Pending";
        e.SortOrder = request.SortOrder ?? 0;

        // One complete set is needed: REST (Client ID + Secret) or classic (username + password + signature).
        var hasRest = e.ClientId is not null && e.SecretKey is not null;
        if (!hasRest && !e.HasClassicCredentials)
            throw new HKPartsEcommerce.Application.Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure(nameof(request.ClientId),
                    "Enter either the REST Client ID and Secret, or the classic API username, password and signature (PayPal Express)."),
            });

        await _db.SaveChangesAsync(ct);
        return PayPalCredentialDto.From(e);
    }
}

public record DeletePayPalCredentialCommand(Guid Id) : IRequest;

public class DeletePayPalCredentialCommandHandler : IRequestHandler<DeletePayPalCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeletePayPalCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeletePayPalCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.PayPalCredentials, _db, request.Id, ct);
}

// ============================ Razorpay =====================================

public class RazorpayCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? KeyId { get; set; }
    public string? KeySecret { get; set; }
    public decimal? TransactionFeePercent { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static RazorpayCredentialDto From(RazorpayCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, KeyId = e.KeyId, KeySecret = e.KeySecret,
        TransactionFeePercent = e.TransactionFeePercent,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListRazorpayCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListRazorpayCredentialsQueryHandler
    : IRequestHandler<ListRazorpayCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListRazorpayCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListRazorpayCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.RazorpayCredentials, ct, c => c.BaseUrl);
}

public record GetRazorpayCredentialQuery(Guid Id) : IRequest<RazorpayCredentialDto>;

public class GetRazorpayCredentialQueryHandler : IRequestHandler<GetRazorpayCredentialQuery, RazorpayCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetRazorpayCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<RazorpayCredentialDto> Handle(GetRazorpayCredentialQuery request, CancellationToken ct)
        => RazorpayCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.RazorpayCredentials, request.Id, ct));
}

public record SaveRazorpayCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? KeyId, string? KeySecret,
    decimal? TransactionFeePercent = null) : IRequest<RazorpayCredentialDto>;

public class SaveRazorpayCredentialCommandValidator : AbstractValidator<SaveRazorpayCredentialCommand>
{
    public SaveRazorpayCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.BaseUrl).NotEmpty().MaximumLength(500)
            .Must(IntegrationCredentialSupport.BeAnAbsoluteHttpUrl)
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://api.razorpay.com/v1");
        RuleFor(x => x.KeyId).NotEmpty();
        RuleFor(x => x.KeySecret).NotEmpty();
        RuleFor(x => x.TransactionFeePercent).InclusiveBetween(0m, 100m).When(x => x.TransactionFeePercent.HasValue);
    }
}

public class SaveRazorpayCredentialCommandHandler : IRequestHandler<SaveRazorpayCredentialCommand, RazorpayCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveRazorpayCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<RazorpayCredentialDto> Handle(SaveRazorpayCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.RazorpayCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.KeyId = IntegrationCredentialSupport.Norm(request.KeyId);
        e.KeySecret = IntegrationCredentialSupport.Norm(request.KeySecret);
        e.TransactionFeePercent = request.TransactionFeePercent;
        await _db.SaveChangesAsync(ct);
        return RazorpayCredentialDto.From(e);
    }
}

public record DeleteRazorpayCredentialCommand(Guid Id) : IRequest;

public class DeleteRazorpayCredentialCommandHandler : IRequestHandler<DeleteRazorpayCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteRazorpayCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteRazorpayCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.RazorpayCredentials, _db, request.Id, ct);
}

// ============================ Square =======================================

public class SquareCredentialDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; }
    public bool IsProduction { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApplicationId { get; set; }
    public string? LocationId { get; set; }
    public string? AccessToken { get; set; }
    public string? ApplicationSecret { get; set; }
    public decimal? TransactionFeePercent { get; set; }

    // PHP parity: the legacy Square method settings.
    public string TransactionType { get; set; } = "AuthCapture";
    /// <summary>Write-only: the last four characters only.</summary>
    public string? RefreshTokenMasked { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public string? DisplayName { get; set; }
    public decimal? MinOrderAmount { get; set; }
    public decimal? MaxOrderAmount { get; set; }
    public List<Guid> AllowedCustomerGroupIds { get; set; } = new();
    public string DefaultOrderStatus { get; set; } = "Pending";
    public int SortOrder { get; set; }

    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }

    public static SquareCredentialDto From(SquareCredential e) => new()
    {
        Id = e.Id, Name = e.Name, Active = e.Active, IsProduction = e.IsProduction,
        BaseUrl = e.BaseUrl, ApplicationId = e.ApplicationId, LocationId = e.LocationId,
        AccessToken = e.AccessToken, ApplicationSecret = e.ApplicationSecret,
        TransactionFeePercent = e.TransactionFeePercent,
        TransactionType = e.TransactionType ?? "AuthCapture",
        RefreshTokenMasked = AuthorizeNetCredentialDto.Mask(e.RefreshToken),
        AccessTokenExpiresAtUtc = e.AccessTokenExpiresAtUtc is DateTime x ? DateTime.SpecifyKind(x, DateTimeKind.Utc) : null,
        DisplayName = e.DisplayName,
        MinOrderAmount = e.MinOrderAmount,
        MaxOrderAmount = e.MaxOrderAmount,
        AllowedCustomerGroupIds = StripeCardTypes.Split(e.AllowedCustomerGroupIds)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList(),
        DefaultOrderStatus = e.DefaultOrderStatus ?? "Pending",
        SortOrder = e.SortOrder,
        CreatedOnUtc = e.CreatedOnUtc, UpdatedOnUtc = e.UpdatedOnUtc,
    };
}

public record ListSquareCredentialsQuery : IRequest<IReadOnlyList<IntegrationCredentialListItemDto>>;

public class ListSquareCredentialsQueryHandler
    : IRequestHandler<ListSquareCredentialsQuery, IReadOnlyList<IntegrationCredentialListItemDto>>
{
    private readonly IApplicationDbContext _db;
    public ListSquareCredentialsQueryHandler(IApplicationDbContext db) => _db = db;
    public Task<IReadOnlyList<IntegrationCredentialListItemDto>> Handle(ListSquareCredentialsQuery request, CancellationToken ct)
        => IntegrationCredentialSupport.ListAsync(_db.SquareCredentials, ct, c => c.BaseUrl);
}

public record GetSquareCredentialQuery(Guid Id) : IRequest<SquareCredentialDto>;

public class GetSquareCredentialQueryHandler : IRequestHandler<GetSquareCredentialQuery, SquareCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public GetSquareCredentialQueryHandler(IApplicationDbContext db) => _db = db;
    public async Task<SquareCredentialDto> Handle(GetSquareCredentialQuery request, CancellationToken ct)
        => SquareCredentialDto.From(await IntegrationCredentialSupport.GetAsync(_db.SquareCredentials, request.Id, ct));
}

public record SaveSquareCredentialCommand(
    Guid? Id, string Name, bool Active, bool IsProduction,
    string? BaseUrl, string? ApplicationId, string? LocationId, string? AccessToken, string? ApplicationSecret,
    decimal? TransactionFeePercent = null,
    string? TransactionType = null,
    string? RefreshToken = null,
    DateTime? AccessTokenExpiresAtUtc = null,
    string? DisplayName = null,
    decimal? MinOrderAmount = null,
    decimal? MaxOrderAmount = null,
    List<Guid>? AllowedCustomerGroupIds = null,
    string? DefaultOrderStatus = null,
    int? SortOrder = null) : IRequest<SquareCredentialDto>;

public class SaveSquareCredentialCommandValidator : AbstractValidator<SaveSquareCredentialCommand>
{
    public SaveSquareCredentialCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // Optional: empty = Square's endpoint for the Mode (Sandbox / Production switch).
        RuleFor(x => x.BaseUrl).MaximumLength(500)
            .Must(u => string.IsNullOrWhiteSpace(u) || IntegrationCredentialSupport.BeAnAbsoluteHttpUrl(u))
            .WithMessage("Base URL must be an absolute http(s) URL, e.g. https://connect.squareupsandbox.com/v2");
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.TransactionFeePercent).InclusiveBetween(0m, 100m).When(x => x.TransactionFeePercent.HasValue);
        RuleFor(x => x.TransactionType)
            .Must(t => string.IsNullOrWhiteSpace(t) || StripeCardTypes.TransactionTypes.Contains(t.Trim()))
            .WithMessage("Transaction type must be AuthCapture or AuthOnly.");
        RuleFor(x => x.MinOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MinOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount).GreaterThanOrEqualTo(0).When(x => x.MaxOrderAmount.HasValue);
        RuleFor(x => x.MaxOrderAmount)
            .GreaterThanOrEqualTo(x => x.MinOrderAmount!.Value)
            .When(x => x.MinOrderAmount.HasValue && x.MaxOrderAmount.HasValue)
            .WithMessage("Maximum order amount must be at least the minimum.");
        RuleFor(x => x.DefaultOrderStatus)
            .Must(s => string.IsNullOrWhiteSpace(s) || StripeCardTypes.OrderStatuses.Contains(s.Trim()))
            .WithMessage("Default order status must be Pending or Processing.");
        RuleFor(x => x.SortOrder).InclusiveBetween(-1000, 1000).When(x => x.SortOrder.HasValue);
    }
}

public class SaveSquareCredentialCommandHandler : IRequestHandler<SaveSquareCredentialCommand, SquareCredentialDto>
{
    private readonly IApplicationDbContext _db;
    public SaveSquareCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public async Task<SquareCredentialDto> Handle(SaveSquareCredentialCommand request, CancellationToken ct)
    {
        var e = await IntegrationCredentialSupport.UpsertAsync(_db.SquareCredentials, request.Id, request.Name, request.Active, request.IsProduction, ct);
        e.BaseUrl = IntegrationCredentialSupport.Norm(request.BaseUrl);
        e.ApplicationId = IntegrationCredentialSupport.Norm(request.ApplicationId);
        e.LocationId = IntegrationCredentialSupport.Norm(request.LocationId);
        e.AccessToken = IntegrationCredentialSupport.Norm(request.AccessToken);
        e.ApplicationSecret = IntegrationCredentialSupport.Norm(request.ApplicationSecret);
        e.TransactionFeePercent = request.TransactionFeePercent;
        e.TransactionType = IntegrationCredentialSupport.Norm(request.TransactionType) ?? "AuthCapture";
        // Write-only: blank keeps the stored refresh token.
        e.RefreshToken = IntegrationCredentialSupport.Norm(request.RefreshToken) ?? e.RefreshToken;
        e.AccessTokenExpiresAtUtc = request.AccessTokenExpiresAtUtc is DateTime exp
            ? (exp.Kind == DateTimeKind.Local ? exp.ToUniversalTime() : DateTime.SpecifyKind(exp, DateTimeKind.Utc))
            : null;
        e.DisplayName = IntegrationCredentialSupport.Norm(request.DisplayName);
        e.MinOrderAmount = request.MinOrderAmount;
        e.MaxOrderAmount = request.MaxOrderAmount;
        e.AllowedCustomerGroupIds = StripeCardTypes.Join(request.AllowedCustomerGroupIds?.Where(g => g != Guid.Empty).Select(g => g.ToString()));
        e.DefaultOrderStatus = IntegrationCredentialSupport.Norm(request.DefaultOrderStatus) ?? "Pending";
        e.SortOrder = request.SortOrder ?? 0;
        await _db.SaveChangesAsync(ct);
        return SquareCredentialDto.From(e);
    }
}

public record DeleteSquareCredentialCommand(Guid Id) : IRequest;

public class DeleteSquareCredentialCommandHandler : IRequestHandler<DeleteSquareCredentialCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteSquareCredentialCommandHandler(IApplicationDbContext db) => _db = db;
    public Task Handle(DeleteSquareCredentialCommand request, CancellationToken ct)
        => IntegrationCredentialSupport.DeleteAsync(_db.SquareCredentials, _db, request.Id, ct);
}


/// <summary>PHP parity, Square "View Locations": test the saved row's token and list its locations.</summary>
public record TestSquareCredentialCommand(Guid Id) : IRequest<GatewayTestResult>;

public class TestSquareCredentialCommandHandler : IRequestHandler<TestSquareCredentialCommand, GatewayTestResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ISquareClient _client;

    public TestSquareCredentialCommandHandler(IApplicationDbContext db, ISquareClient client)
    {
        _db = db;
        _client = client;
    }

    public async Task<GatewayTestResult> Handle(TestSquareCredentialCommand request, CancellationToken ct)
    {
        if (!await _db.SquareCredentials.AsNoTracking().AnyAsync(x => x.Id == request.Id, ct))
            throw new HKPartsEcommerce.Application.Common.Exceptions.NotFoundException(nameof(SquareCredential), request.Id);
        return await _client.ListLocationsAsync(request.Id, ct);
    }
}
