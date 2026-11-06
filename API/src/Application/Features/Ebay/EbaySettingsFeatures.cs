using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Ebay;

// ---- Get settings ----------------------------------------------------------------------------------------

public record GetEbaySettingsQuery(Guid? StoreId) : IRequest<EbaySettingsDto>;

public class GetEbaySettingsHandler : IRequestHandler<GetEbaySettingsQuery, EbaySettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayOAuthClient _oauth;
    private readonly IDateTimeProvider _clock;

    public GetEbaySettingsHandler(IApplicationDbContext db, IEbayOAuthClient oauth, IDateTimeProvider clock)
    {
        _db = db;
        _oauth = oauth;
        _clock = clock;
    }

    public async Task<EbaySettingsDto> Handle(GetEbaySettingsQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct)
                      ?? new EbayAccount { StoreId = storeId };
        var storeName = await _db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => s.Name).FirstOrDefaultAsync(ct);

        var now = _clock.UtcNow;
        var state = string.IsNullOrEmpty(account.RefreshToken) && string.IsNullOrEmpty(account.AccessToken)
            ? EbayConnectionState.NotConnected
            : (account.RefreshTokenExpiresAtUtc is DateTime refreshExpiry && refreshExpiry <= now)
              || (string.IsNullOrEmpty(account.RefreshToken) && account.AccessTokenExpiresAtUtc <= now)
                ? EbayConnectionState.TokenExpired
                : EbayConnectionState.Connected;

        string? categoryName = null;
        if (!string.IsNullOrEmpty(account.DefaultCategoryId))
            categoryName = await _db.EbayCategories.AsNoTracking()
                .Where(c => c.CategoryId == account.DefaultCategoryId).Select(c => c.Name).FirstOrDefaultAsync(ct);

        var tree = await _db.EbayCategories.AsNoTracking()
            .GroupBy(c => c.TreeVersion).Select(g => new { Version = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).FirstOrDefaultAsync(ct);

        return new EbaySettingsDto
        {
            StoreId = storeId,
            StoreName = storeName,
            ConnectionState = state,
            ConnectedEbayUser = account.ConnectedEbayUser,
            ConnectedAtUtc = account.ConnectedAtUtc,
            AppCredentialsConfigured = _oauth.IsConfigured(account.Environment),
            IsEnabled = account.IsEnabled,
            Environment = account.Environment,
            MarketplaceId = account.MarketplaceId,
            LoggingEnabled = account.LoggingEnabled,
            DefaultCategoryId = account.DefaultCategoryId,
            DefaultCategoryName = categoryName,
            DefaultPaymentPolicyId = account.DefaultPaymentPolicyId,
            DefaultFulfillmentPolicyId = account.DefaultFulfillmentPolicyId,
            DefaultReturnPolicyId = account.DefaultReturnPolicyId,
            MerchantLocationKey = account.MerchantLocationKey,
            FitmentEnabled = account.FitmentEnabled,
            SyncPriceFromStore = account.SyncPriceFromStore,
            OrdersLastSyncedAtUtc = account.OrdersLastSyncedAtUtc,
            CategoryCount = tree?.Count ?? 0,
            CategoryTreeVersion = tree?.Version,
        };
    }
}

// ---- Save settings ---------------------------------------------------------------------------------------

public record SaveEbaySettingsCommand(
    Guid? StoreId,
    bool IsEnabled,
    EbayEnvironment Environment,
    string MarketplaceId,
    bool LoggingEnabled,
    string? DefaultCategoryId,
    string? DefaultPaymentPolicyId,
    string? DefaultFulfillmentPolicyId,
    string? DefaultReturnPolicyId,
    bool FitmentEnabled,
    bool SyncPriceFromStore) : IRequest<EbaySettingsDto>;

public class SaveEbaySettingsValidator : AbstractValidator<SaveEbaySettingsCommand>
{
    public SaveEbaySettingsValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.MarketplaceId).NotEmpty().MaximumLength(32).Matches("^EBAY_[A-Z_]+$")
            .WithMessage("Marketplace must be an eBay marketplace id such as EBAY_US.");
        RuleFor(x => x.DefaultCategoryId)
            .MustAsync(async (id, ct) => await db.EbayCategories.AnyAsync(c => c.CategoryId == id && c.IsLeaf, ct))
            .When(x => !string.IsNullOrWhiteSpace(x.DefaultCategoryId))
            .WithMessage("The default category must be an eBay leaf category.");
    }
}

public class SaveEbaySettingsHandler : IRequestHandler<SaveEbaySettingsCommand, EbaySettingsDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public SaveEbaySettingsHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<EbaySettingsDto> Handle(SaveEbaySettingsCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var account = await _db.EbayAccounts.FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null)
        {
            account = new EbayAccount { StoreId = storeId };
            _db.EbayAccounts.Add(account);
        }

        // Tokens belong to one environment/marketplace; switching drops the connection and cached tree.
        if (account.Environment != request.Environment || !string.Equals(account.MarketplaceId, request.MarketplaceId, StringComparison.Ordinal))
        {
            account.AccessToken = null;
            account.AccessTokenExpiresAtUtc = null;
            account.RefreshToken = null;
            account.RefreshTokenExpiresAtUtc = null;
            account.ConnectedEbayUser = null;
            account.ConnectedAtUtc = null;
            account.CategoryTreeId = null;
            account.DefaultPaymentPolicyId = null;
            account.DefaultFulfillmentPolicyId = null;
            account.DefaultReturnPolicyId = null;
            account.MerchantLocationKey = null;
        }
        else
        {
            account.DefaultPaymentPolicyId = Blank(request.DefaultPaymentPolicyId);
            account.DefaultFulfillmentPolicyId = Blank(request.DefaultFulfillmentPolicyId);
            account.DefaultReturnPolicyId = Blank(request.DefaultReturnPolicyId);
        }

        account.IsEnabled = request.IsEnabled;
        account.Environment = request.Environment;
        account.MarketplaceId = request.MarketplaceId;
        account.LoggingEnabled = request.LoggingEnabled;
        account.DefaultCategoryId = Blank(request.DefaultCategoryId);
        account.FitmentEnabled = request.FitmentEnabled;
        account.SyncPriceFromStore = request.SyncPriceFromStore;

        await _db.SaveChangesAsync(ct);
        return await _mediator.Send(new GetEbaySettingsQuery(storeId), ct);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// ---- OAuth -----------------------------------------------------------------------------------------------

public record GetEbayConnectUrlQuery(Guid? StoreId) : IRequest<string>;

public class GetEbayConnectUrlHandler : IRequestHandler<GetEbayConnectUrlQuery, string>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayOAuthClient _oauth;
    private readonly IEbayOAuthStateProtector _state;
    private readonly ICurrentUserService _user;

    public GetEbayConnectUrlHandler(
        IApplicationDbContext db, IEbayOAuthClient oauth, IEbayOAuthStateProtector state, ICurrentUserService user)
    {
        _db = db;
        _oauth = oauth;
        _state = state;
        _user = user;
    }

    public async Task<string> Handle(GetEbayConnectUrlQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var environment = await _db.EbayAccounts.AsNoTracking()
            .Where(a => a.StoreId == storeId).Select(a => (EbayEnvironment?)a.Environment).FirstOrDefaultAsync(ct)
            ?? EbayEnvironment.Sandbox;
        return _oauth.BuildAuthorizeUrl(environment, _state.Create(storeId, _user.UserId));
    }
}

/// <summary>The eBay redirect: validate <c>state</c>, exchange the code, store both tokens. Returns the store id.</summary>
public record CompleteEbayOAuthCommand(string Code, string State) : IRequest<Guid>;

public class CompleteEbayOAuthHandler : IRequestHandler<CompleteEbayOAuthCommand, Guid>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayOAuthClient _oauth;
    private readonly IEbayOAuthStateProtector _state;
    private readonly IEbayClient _ebay;
    private readonly IDateTimeProvider _clock;

    public CompleteEbayOAuthHandler(
        IApplicationDbContext db, IEbayOAuthClient oauth, IEbayOAuthStateProtector state, IEbayClient ebay, IDateTimeProvider clock)
    {
        _db = db;
        _oauth = oauth;
        _state = state;
        _ebay = ebay;
        _clock = clock;
    }

    public async Task<Guid> Handle(CompleteEbayOAuthCommand request, CancellationToken ct)
    {
        var storeId = _state.Validate(request.State);
        var account = await _db.EbayAccounts.FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null)
        {
            account = new EbayAccount { StoreId = storeId };
            _db.EbayAccounts.Add(account);
        }

        EbayTokenResult tokens;
        try
        {
            tokens = await _oauth.ExchangeCodeAsync(account.Environment, request.Code, ct);
        }
        catch (EbayApiException ex)
        {
            throw new ServiceUnavailableException($"eBay did not accept the authorization: {ex.Message}");
        }

        account.AccessToken = tokens.AccessToken;
        account.AccessTokenExpiresAtUtc = tokens.AccessTokenExpiresAtUtc;
        account.RefreshToken = tokens.RefreshToken;
        account.RefreshTokenExpiresAtUtc = tokens.RefreshTokenExpiresAtUtc;
        account.ConnectedAtUtc = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        // Best-effort niceties: who connected, and the marketplace category tree id.
        try
        {
            account.ConnectedEbayUser = await _ebay.GetUsernameAsync(storeId, ct);
        }
        catch (Exception ex) when (ex is EbayApiException or ServiceUnavailableException) { }

        try
        {
            account.CategoryTreeId ??= await _ebay.GetDefaultCategoryTreeIdAsync(storeId, ct);
        }
        catch (Exception ex) when (ex is EbayApiException or ServiceUnavailableException) { }

        await _db.SaveChangesAsync(ct);
        return storeId;
    }
}

public record DisconnectEbayCommand(Guid? StoreId) : IRequest;

public class DisconnectEbayHandler : IRequestHandler<DisconnectEbayCommand>
{
    private readonly IApplicationDbContext _db;

    public DisconnectEbayHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DisconnectEbayCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var account = await _db.EbayAccounts.FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        if (account is null) return;

        account.AccessToken = null;
        account.AccessTokenExpiresAtUtc = null;
        account.RefreshToken = null;
        account.RefreshTokenExpiresAtUtc = null;
        account.ConnectedEbayUser = null;
        account.ConnectedAtUtc = null;
        await _db.SaveChangesAsync(ct);
    }
}

// ---- Policies / location ---------------------------------------------------------------------------------

public record GetEbayPoliciesQuery(Guid? StoreId, EbayPolicyType Type, bool Refresh) : IRequest<IReadOnlyList<EbayPolicy>>;

public class GetEbayPoliciesHandler : IRequestHandler<GetEbayPoliciesQuery, IReadOnlyList<EbayPolicy>>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayMetadataService _metadata;

    public GetEbayPoliciesHandler(IApplicationDbContext db, IEbayMetadataService metadata)
    {
        _db = db;
        _metadata = metadata;
    }

    public async Task<IReadOnlyList<EbayPolicy>> Handle(GetEbayPoliciesQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        try
        {
            return await _metadata.GetPoliciesAsync(storeId, request.Type, request.Refresh, ct);
        }
        catch (EbayApiException ex)
        {
            throw EbaySupport.BusinessError(ex);
        }
    }
}

/// <summary>Creates or updates the eBay merchant location from the store's shipper address; saves its key.</summary>
public record SyncEbayLocationCommand(Guid? StoreId) : IRequest<string>;

public class SyncEbayLocationHandler : IRequestHandler<SyncEbayLocationCommand, string>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayClient _ebay;

    public SyncEbayLocationHandler(IApplicationDbContext db, IEbayClient ebay)
    {
        _db = db;
        _ebay = ebay;
    }

    public async Task<string> Handle(SyncEbayLocationCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var store = await _db.Stores.AsNoTracking().Include(s => s.Address).FirstAsync(s => s.Id == storeId, ct);
        var account = await _db.EbayAccounts.FirstOrDefaultAsync(a => a.StoreId == storeId, ct)
                      ?? throw new ServiceUnavailableException("Connect eBay before syncing the inventory location.");

        if (store.Address is null || string.IsNullOrWhiteSpace(store.Address.AddressLine1)
            || string.IsNullOrWhiteSpace(store.Address.City) || string.IsNullOrWhiteSpace(store.Address.PostalCode)
            || string.IsNullOrWhiteSpace(store.Address.CountryCode))
            throw new Common.Exceptions.ValidationException(new[]
            {
                new FluentValidation.Results.ValidationFailure("Address",
                    "The store's shipper address is incomplete (street, city, postal code and country are required). Edit the store first."),
            });

        var key = account.MerchantLocationKey ?? $"hk-{storeId:N}";
        try
        {
            await _ebay.CreateOrReplaceLocationAsync(storeId, key, new EbayAddress(
                store.Name, store.Address.AddressLine1, store.Address.AddressLine2, store.Address.City,
                store.Address.StateProvince, store.Address.PostalCode, store.Address.CountryCode, store.ContactPhone),
                store.Name, ct);
        }
        catch (EbayApiException ex)
        {
            throw EbaySupport.BusinessError(ex);
        }

        account.MerchantLocationKey = key;
        await _db.SaveChangesAsync(ct);
        return key;
    }
}

// ---- Categories ------------------------------------------------------------------------------------------

public record RefreshEbayCategoriesCommand(Guid? StoreId) : IRequest;

public class RefreshEbayCategoriesHandler : IRequestHandler<RefreshEbayCategoriesCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayJobQueue _jobs;
    private readonly IEbayAuthService _auth;

    public RefreshEbayCategoriesHandler(IApplicationDbContext db, IEbayJobQueue jobs, IEbayAuthService auth)
    {
        _db = db;
        _jobs = jobs;
        _auth = auth;
    }

    public async Task Handle(RefreshEbayCategoriesCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        // Fail fast (503) when not connected rather than queueing a job that cannot run.
        await _auth.GetSessionAsync(storeId, allowDisabled: true, ct);
        _jobs.EnqueueCategoryRefresh(storeId);
    }
}

/// <summary>Children of a node (roots when <c>ParentId</c> is null), or — with <c>Search</c> — up to 100 matches with their path.</summary>
public record ListEbayCategoriesQuery(string? ParentId, string? Search) : IRequest<List<EbayCategoryDto>>;

public class ListEbayCategoriesHandler : IRequestHandler<ListEbayCategoriesQuery, List<EbayCategoryDto>>
{
    private readonly IApplicationDbContext _db;

    public ListEbayCategoriesHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<EbayCategoryDto>> Handle(ListEbayCategoriesQuery request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var matches = await _db.EbayCategories.AsNoTracking()
                .Where(c => c.Name.Contains(term))
                .OrderByDescending(c => c.IsLeaf).ThenBy(c => c.Level).ThenBy(c => c.Name)
                .Take(100)
                .ToListAsync(ct);

            // Resolve breadcrumbs: walk the ancestor chains level by level (≤ ~7 levels).
            var names = new Dictionary<string, (string Name, string? Parent)>();
            var pending = matches.Select(m => m.ParentCategoryId).Where(p => p != null).Select(p => p!).ToHashSet();
            while (pending.Count > 0)
            {
                var batch = pending.ToList();
                var parents = await _db.EbayCategories.AsNoTracking()
                    .Where(c => batch.Contains(c.CategoryId))
                    .Select(c => new { c.CategoryId, c.Name, c.ParentCategoryId })
                    .ToListAsync(ct);
                pending.Clear();
                foreach (var p in parents)
                {
                    names[p.CategoryId] = (p.Name, p.ParentCategoryId);
                    if (p.ParentCategoryId != null && !names.ContainsKey(p.ParentCategoryId))
                        pending.Add(p.ParentCategoryId);
                }

                if (parents.Count == 0) break;
            }

            return matches.Select(m =>
            {
                var dto = EbayCategoryDto.From(m);
                var chain = new List<string> { m.Name };
                var parent = m.ParentCategoryId;
                while (parent != null && names.TryGetValue(parent, out var node))
                {
                    chain.Insert(0, node.Name);
                    parent = node.Parent;
                }

                dto.Path = string.Join(" > ", chain);
                return dto;
            }).ToList();
        }

        var children = await _db.EbayCategories.AsNoTracking()
            .Where(c => c.ParentCategoryId == request.ParentId)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return children.Select(EbayCategoryDto.From).ToList();
    }
}

public record GetEbayCategoryAspectsQuery(Guid? StoreId, string CategoryId) : IRequest<IReadOnlyList<EbayAspectDefinition>>;

public class GetEbayCategoryAspectsHandler : IRequestHandler<GetEbayCategoryAspectsQuery, IReadOnlyList<EbayAspectDefinition>>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayMetadataService _metadata;

    public GetEbayCategoryAspectsHandler(IApplicationDbContext db, IEbayMetadataService metadata)
    {
        _db = db;
        _metadata = metadata;
    }

    public async Task<IReadOnlyList<EbayAspectDefinition>> Handle(GetEbayCategoryAspectsQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        try { return await _metadata.GetAspectsAsync(storeId, request.CategoryId, ct); }
        catch (EbayApiException ex) { throw EbaySupport.BusinessError(ex); }
    }
}

public record GetEbayCategoryConditionsQuery(Guid? StoreId, string CategoryId) : IRequest<List<EbayItemConditionDto>>;

public class GetEbayCategoryConditionsHandler : IRequestHandler<GetEbayCategoryConditionsQuery, List<EbayItemConditionDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayMetadataService _metadata;

    public GetEbayCategoryConditionsHandler(IApplicationDbContext db, IEbayMetadataService metadata)
    {
        _db = db;
        _metadata = metadata;
    }

    public async Task<List<EbayItemConditionDto>> Handle(GetEbayCategoryConditionsQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        try
        {
            var conditions = await _metadata.GetConditionsAsync(storeId, request.CategoryId, ct);
            return conditions.Select(c => new EbayItemConditionDto { ConditionId = c.ConditionId, Description = c.Description }).ToList();
        }
        catch (EbayApiException ex) { throw EbaySupport.BusinessError(ex); }
    }
}

public record GetEbayCategoryCompatibilityQuery(Guid? StoreId, string CategoryId) : IRequest<EbayCompatibilityInfo>;

public class GetEbayCategoryCompatibilityHandler : IRequestHandler<GetEbayCategoryCompatibilityQuery, EbayCompatibilityInfo>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayMetadataService _metadata;

    public GetEbayCategoryCompatibilityHandler(IApplicationDbContext db, IEbayMetadataService metadata)
    {
        _db = db;
        _metadata = metadata;
    }

    public async Task<EbayCompatibilityInfo> Handle(GetEbayCategoryCompatibilityQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        try { return await _metadata.GetCompatibilityAsync(storeId, request.CategoryId, ct); }
        catch (EbayApiException ex) { throw EbaySupport.BusinessError(ex); }
    }
}
