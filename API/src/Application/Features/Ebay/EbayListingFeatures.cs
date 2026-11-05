using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Ebay;

// ---- Step 1: eligible products ---------------------------------------------------------------------------

public record ListEligibleEbayProductsQuery(Guid? StoreId, string? Search) : IRequest<List<EbayEligibleProductDto>>;

public class ListEligibleEbayProductsHandler : IRequestHandler<ListEligibleEbayProductsQuery, List<EbayEligibleProductDto>>
{
    private readonly IApplicationDbContext _db;

    public ListEligibleEbayProductsHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<EbayEligibleProductDto>> Handle(ListEligibleEbayProductsQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var query = EbaySupport.EligibleProducts(_db, storeId).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || (p.Sku != null && p.Sku.Contains(term)));
        }

        return await query
            .OrderBy(p => p.Name)
            .Take(50)
            .Select(p => new EbayEligibleProductDto
            {
                Id = p.Id,
                Sku = p.Sku,
                Name = p.Name,
                ProductType = p.ProductType,
                Price = p.Price,
                StoreQuantity = _db.InventoryLevels
                    .Where(l => l.StoreId == storeId && l.ProductId == p.Id && l.ProductVariantId == null)
                    .Sum(l => (int?)l.StockQuantity) ?? 0,
            })
            .ToListAsync(ct);
    }
}

// ---- List ------------------------------------------------------------------------------------------------

public record ListEbayListingsQuery(
    Guid? StoreId, int Page = 1, int PageSize = 25, string? Search = null, EbayListingStatus? Status = null,
    bool IncludeEnded = false) : IRequest<PaginatedList<EbayListingListItemDto>>;

public class ListEbayListingsHandler : IRequestHandler<ListEbayListingsQuery, PaginatedList<EbayListingListItemDto>>
{
    private readonly IApplicationDbContext _db;

    public ListEbayListingsHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<EbayListingListItemDto>> Handle(ListEbayListingsQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var environment = await _db.EbayAccounts.AsNoTracking().Where(a => a.StoreId == storeId)
            .Select(a => (EbayEnvironment?)a.Environment).FirstOrDefaultAsync(ct) ?? EbayEnvironment.Sandbox;

        var query = _db.EbayListings.AsNoTracking().Where(l => l.StoreId == storeId);
        if (request.Status is EbayListingStatus status)
            query = query.Where(l => l.Status == status);
        else if (!request.IncludeEnded)
            query = query.Where(l => l.Status != EbayListingStatus.Ended);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l => l.Title.Contains(term) || l.Sku.Contains(term));
        }

        // Active first, then Draft and Error, then Ended; Title A–Z within each.
        var ordered = query
            .OrderBy(l => l.Status == EbayListingStatus.Active ? 0
                : l.Status == EbayListingStatus.Draft ? 1
                : l.Status == EbayListingStatus.Error ? 2 : 3)
            .ThenBy(l => l.Title);

        var projected = ordered.Select(l => new EbayListingListItemDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            Title = l.Title,
            Sku = l.Sku,
            StoreInventory = _db.InventoryLevels
                .Where(i => i.StoreId == storeId && i.ProductId == l.ProductId && i.ProductVariantId == null)
                .Sum(i => (int?)i.StockQuantity) ?? 0,
            Quantity = l.Quantity,
            QuantitySold = l.QuantitySold,
            Price = l.Price,
            Status = l.Status,
            LastError = l.LastError,
            EbayItemId = l.EbayListingIdExternal,
            LastSyncedAtUtc = l.LastSyncedAtUtc,
            EndedAtUtc = l.EndedAtUtc,
        });

        var page = await PaginatedList<EbayListingListItemDto>.CreateAsync(
            projected, request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
        foreach (var item in page.Items)
            item.EbayItemUrl = EbaySupport.ItemUrl(environment, item.EbayItemId);
        return page;
    }
}

// ---- Step 3: create draft --------------------------------------------------------------------------------

public record CreateEbayListingCommand(Guid? StoreId, Guid ProductId, string CategoryId) : IRequest<EbayListingDto>;

public class CreateEbayListingValidator : AbstractValidator<CreateEbayListingCommand>
{
    public CreateEbayListingValidator(IApplicationDbContext db)
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.CategoryId).NotEmpty().WithMessage("Select an eBay category.")
            .MustAsync(async (id, ct) => await db.EbayCategories.AnyAsync(c => c.CategoryId == id && c.IsLeaf, ct))
            .WithMessage("Select a leaf eBay category (one with no sub-categories).");
    }
}

public class CreateEbayListingHandler : IRequestHandler<CreateEbayListingCommand, EbayListingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPriceResolver _prices;
    private readonly IEbayPublicUrlProvider _urls;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public CreateEbayListingHandler(
        IApplicationDbContext db, IPriceResolver prices, IEbayPublicUrlProvider urls, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _prices = prices;
        _urls = urls;
        _user = user;
        _clock = clock;
    }

    public async Task<EbayListingDto> Handle(CreateEbayListingCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
                      ?? throw new NotFoundException(nameof(Product), request.ProductId);

        if (await _db.EbayListings.AnyAsync(l => l.ProductId == product.Id && l.Status != EbayListingStatus.Ended, ct))
            throw new ConflictException(EbaySupport.AlreadyListedMessage);

        if (!await EbaySupport.EligibleProducts(_db, storeId).AnyAsync(p => p.Id == product.Id, ct))
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.ProductId), EbaySupport.NotEligibleMessage) });

        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        var price = await _prices.GetPriceAsync(product.Id, null, customerGroupId: null, storeId, cancellationToken: ct);
        var quantity = await EbaySupport.StoreQuantity(_db, storeId, product.Id).SumAsync(ct);

        var listing = new EbayListing
        {
            StoreId = storeId,
            ProductId = product.Id,
            Sku = EbaySupport.Truncate(string.IsNullOrWhiteSpace(product.Sku) ? product.Id.ToString("N") : product.Sku, 50),
            CategoryId = request.CategoryId,
            Status = EbayListingStatus.Draft,
            Title = EbaySupport.Truncate(product.Name, EbaySupport.MaxTitleLength),
            Description = string.IsNullOrWhiteSpace(product.FullDescription) ? product.ShortDescription : product.FullDescription,
            Price = Math.Round(price.FinalPrice, 2, MidpointRounding.AwayFromZero),
            Quantity = Math.Max(0, quantity),
            PaymentPolicyId = account?.DefaultPaymentPolicyId,
            FulfillmentPolicyId = account?.DefaultFulfillmentPolicyId,
            ReturnPolicyId = account?.DefaultReturnPolicyId,
        };
        _db.EbayListings.Add(listing);
        product.ChannelEbay = true;

        _db.AuditTrails.Add(EbaySupport.Audit("EbayListing.Created", listing.Id, null,
            new { listing.Title, listing.Sku, listing.CategoryId, listing.Price, listing.Quantity }, _user, _clock));

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost a race against another admin — the filtered unique index caught it.
            if (await _db.EbayListings.AsNoTracking().AnyAsync(
                    l => l.ProductId == product.Id && l.Status != EbayListingStatus.Ended && l.Id != listing.Id, ct))
                throw new ConflictException(EbaySupport.AlreadyListedMessage);
            throw;
        }

        return await EbayListingReader.LoadAsync(_db, _urls, storeId, listing.Id, ct);
    }
}

// ---- Get -------------------------------------------------------------------------------------------------

public record GetEbayListingQuery(Guid? StoreId, Guid Id) : IRequest<EbayListingDto>;

public class GetEbayListingHandler : IRequestHandler<GetEbayListingQuery, EbayListingDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayPublicUrlProvider _urls;

    public GetEbayListingHandler(IApplicationDbContext db, IEbayPublicUrlProvider urls)
    {
        _db = db;
        _urls = urls;
    }

    public async Task<EbayListingDto> Handle(GetEbayListingQuery request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        return await EbayListingReader.LoadAsync(_db, _urls, storeId, request.Id, ct);
    }
}

/// <summary>Builds the full listing DTO (aspects, images, fitment). Shared by the get/create/save handlers.</summary>
public static class EbayListingReader
{
    public static async Task<EbayListingDto> LoadAsync(
        IApplicationDbContext _db, IEbayPublicUrlProvider _urls, Guid storeId, Guid id, CancellationToken ct)
    {
        var listing = await _db.EbayListings.AsNoTracking()
            .Include(l => l.Aspects)
            .Include(l => l.Product)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id && l.StoreId == storeId, ct)
            ?? throw new NotFoundException(nameof(EbayListing), id);

        var account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
        var environment = account?.Environment ?? EbayEnvironment.Sandbox;

        var images = await _db.ProductPictures.AsNoTracking()
            .Where(p => p.ProductId == listing.ProductId && p.ProductVariantId == null && p.Media != null && p.Media.MediaType == MediaType.Image)
            .OrderBy(p => p.DisplayOrder)
            .Take(EbaySupport.MaxImages)
            .Select(p => new { p.Media!.Url, p.Media.AltText })
            .ToListAsync(ct);

        var fitment = await _db.VehicleFitments.AsNoTracking()
            .Where(f => f.ProductId == listing.ProductId)
            .OrderBy(f => f.Year).ThenBy(f => f.Make).ThenBy(f => f.Model)
            .Select(f => new EbayFitmentDto { Year = f.Year, Make = f.Make, Model = f.Model, Trim = f.Trim, Engine = f.Engine })
            .ToListAsync(ct);

        var categoryName = await _db.EbayCategories.AsNoTracking()
            .Where(c => c.CategoryId == listing.CategoryId).Select(c => c.Name).FirstOrDefaultAsync(ct);

        return new EbayListingDto
        {
            Id = listing.Id,
            StoreId = listing.StoreId,
            ProductId = listing.ProductId,
            ProductName = listing.Product?.Name ?? string.Empty,
            Sku = listing.Sku,
            CategoryId = listing.CategoryId,
            CategoryName = categoryName,
            Status = listing.Status,
            IsPublished = !string.IsNullOrEmpty(listing.EbayListingIdExternal),
            Title = listing.Title,
            Subtitle = listing.Subtitle,
            Description = listing.Description,
            Price = listing.Price,
            Quantity = listing.Quantity,
            QuantitySold = listing.QuantitySold,
            StoreInventory = await EbaySupport.StoreQuantity(_db, storeId, listing.ProductId).SumAsync(ct),
            ConditionId = listing.ConditionId,
            PaymentPolicyId = listing.PaymentPolicyId,
            FulfillmentPolicyId = listing.FulfillmentPolicyId,
            ReturnPolicyId = listing.ReturnPolicyId,
            OfferId = listing.OfferId,
            EbayItemId = listing.EbayListingIdExternal,
            EbayItemUrl = EbaySupport.ItemUrl(environment, listing.EbayListingIdExternal),
            LastError = listing.LastError,
            PublishedAtUtc = listing.PublishedAtUtc,
            LastSyncedAtUtc = listing.LastSyncedAtUtc,
            EndedAtUtc = listing.EndedAtUtc,
            Aspects = listing.Aspects
                .GroupBy(a => a.Name)
                .OrderBy(g => g.Key)
                .Select(g => new EbayListingAspectValueDto { Name = g.Key, Values = g.OrderBy(a => a.Id).Select(a => a.Value).ToList() })
                .ToList(),
            Images = images
                .Select(i => new EbayListingImageDto { Url = _urls.ToPublicImageUrl(i.Url) ?? i.Url ?? string.Empty, AltText = i.AltText })
                .Where(i => i.Url.Length > 0)
                .ToList(),
            FitmentEnabled = account?.FitmentEnabled ?? false,
            Fitment = fitment,
            RowVersion = Convert.ToBase64String(listing.RowVersion ?? Array.Empty<byte>()),
        };
    }
}

// ---- Save (local → publish / revise) ---------------------------------------------------------------------

public sealed record EbayAspectInput(string Name, List<string>? Values);

public record SaveEbayListingCommand(
    Guid? StoreId,
    Guid Id,
    EbayListingStatus Status,
    string Title,
    string? Subtitle,
    string Sku,
    decimal Price,
    int Quantity,
    string? ConditionId,
    string? Description,
    string? PaymentPolicyId,
    string? FulfillmentPolicyId,
    string? ReturnPolicyId,
    List<EbayAspectInput>? Aspects,
    string? RowVersion) : IRequest<EbayListingDto>;

public class SaveEbayListingValidator : AbstractValidator<SaveEbayListingCommand>
{
    public SaveEbayListingValidator()
    {
        RuleFor(x => x.Status).Must(s => s is EbayListingStatus.Draft or EbayListingStatus.Active)
            .WithMessage("Status must be Draft or Active. Use End to end a listing.");
        RuleFor(x => x.Title).NotEmpty().WithMessage("Title is required.").MaximumLength(80);
        RuleFor(x => x.Subtitle).MaximumLength(55);
        RuleFor(x => x.Sku).NotEmpty().WithMessage("SKU is required.").MaximumLength(50);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0.");
        RuleFor(x => x.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ConditionId).NotEmpty().WithMessage("Condition is required.");
        RuleFor(x => x.Description).NotEmpty().WithMessage("Description is required.");
        RuleFor(x => x.PaymentPolicyId).NotEmpty().WithMessage("Payment policy is required.");
        RuleFor(x => x.FulfillmentPolicyId).NotEmpty().WithMessage("Shipping policy is required.");
        RuleFor(x => x.ReturnPolicyId).NotEmpty().WithMessage("Return policy is required.");
        RuleFor(x => x.RowVersion).NotEmpty().WithMessage("rowVersion is required.");
        RuleForEach(x => x.Aspects).ChildRules(a =>
        {
            a.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            a.RuleForEach(x => x.Values).MaximumLength(500);
        });
    }
}

public class SaveEbayListingHandler : IRequestHandler<SaveEbayListingCommand, EbayListingDto>
{
    public const string VerificationErrorTitle = "ERROR - Your listing has valid eBay verification:";
    private const string ConflictMessage = "This listing was changed by someone else. Reload?";

    private readonly IApplicationDbContext _db;
    private readonly IEbayListingService _listings;
    private readonly IEbayMetadataService _metadata;
    private readonly IEbayPublicUrlProvider _urls;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public SaveEbayListingHandler(
        IApplicationDbContext db, IEbayListingService listings, IEbayMetadataService metadata, IEbayPublicUrlProvider urls,
        ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _listings = listings;
        _metadata = metadata;
        _urls = urls;
        _user = user;
        _clock = clock;
    }

    public async Task<EbayListingDto> Handle(SaveEbayListingCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var listing = await _db.EbayListings.Include(l => l.Aspects)
            .FirstOrDefaultAsync(l => l.Id == request.Id && l.StoreId == storeId, ct)
            ?? throw new NotFoundException(nameof(EbayListing), request.Id);

        if (listing.Status == EbayListingStatus.Ended)
            throw new ConflictException("This listing has ended. List the product again to create a new listing.");

        byte[] expected;
        try { expected = Convert.FromBase64String(request.RowVersion!); }
        catch (FormatException) { throw new ConflictException(ConflictMessage); }
        if (!listing.RowVersion.AsSpan().SequenceEqual(expected))
            throw new ConflictException(ConflictMessage);

        var published = !string.IsNullOrEmpty(listing.EbayListingIdExternal);
        var failures = new List<ValidationFailure>();
        if (published && request.Status == EbayListingStatus.Draft)
            failures.Add(new ValidationFailure(nameof(request.Status), "A published listing cannot go back to Draft. Use End instead."));
        if (published && !string.Equals(request.Sku.Trim(), listing.Sku, StringComparison.Ordinal))
            failures.Add(new ValidationFailure(nameof(request.Sku), "The SKU cannot change once the listing is published."));

        var aspects = (request.Aspects ?? new List<EbayAspectInput>())
            .Where(a => !string.IsNullOrWhiteSpace(a.Name))
            .Select(a => (Name: a.Name.Trim(), Values: (a.Values ?? new List<string>())
                .Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).Distinct().ToList()))
            .Where(a => a.Values.Count > 0)
            .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Name: g.First().Name, Values: g.SelectMany(x => x.Values).Distinct().ToList()))
            .ToList();

        EbayAccount? account = null;
        if (request.Status == EbayListingStatus.Active)
        {
            // Everything that would make eBay reject the publish is checked before the first eBay write.
            account = await _db.EbayAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.StoreId == storeId, ct);
            if (account is null || !account.IsEnabled)
                throw new ServiceUnavailableException("eBay is not connected or is disabled for this store.");
            if (string.IsNullOrEmpty(account.MerchantLocationKey))
                failures.Add(new ValidationFailure("MerchantLocationKey",
                    "Set up the eBay inventory location in eBay Settings before publishing."));

            IReadOnlyList<EbayAspectDefinition> definitions;
            try { definitions = await _metadata.GetAspectsAsync(storeId, listing.CategoryId, ct); }
            catch (EbayApiException ex) { throw new ExternalBusinessException(VerificationErrorTitle, ex.Errors); }

            foreach (var required in definitions.Where(d => d.Required))
            {
                if (!aspects.Any(a => string.Equals(a.Name, required.Name, StringComparison.OrdinalIgnoreCase)))
                    failures.Add(new ValidationFailure($"Aspects[{required.Name}]", $"{required.Name} is required by eBay for this category."));
            }

            foreach (var single in definitions.Where(d => !d.Multiple))
            {
                var given = aspects.FirstOrDefault(a => string.Equals(a.Name, single.Name, StringComparison.OrdinalIgnoreCase));
                if (given.Values is { Count: > 1 })
                    failures.Add(new ValidationFailure($"Aspects[{single.Name}]", $"{single.Name} accepts a single value."));
            }
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        var before = (await EbayListingReader.LoadAsync(_db, _urls, storeId, listing.Id, ct)).ToAuditSnapshot();

        // 1. Save the local record.
        listing.Title = request.Title.Trim();
        listing.Subtitle = string.IsNullOrWhiteSpace(request.Subtitle) ? null : request.Subtitle.Trim();
        if (!published) listing.Sku = request.Sku.Trim();
        listing.Price = Math.Round(request.Price, 2, MidpointRounding.AwayFromZero);
        listing.Quantity = request.Quantity;
        listing.ConditionId = request.ConditionId;
        listing.Description = request.Description;
        listing.PaymentPolicyId = request.PaymentPolicyId;
        listing.FulfillmentPolicyId = request.FulfillmentPolicyId;
        listing.ReturnPolicyId = request.ReturnPolicyId;
        if (request.Status == EbayListingStatus.Draft)
            listing.Status = EbayListingStatus.Draft;
        listing.UpdatedOnUtc = _clock.UtcNow;

        foreach (var old in listing.Aspects.ToList())
            listing.Aspects.Remove(old);
        foreach (var (name, values) in aspects)
            foreach (var value in values)
                listing.Aspects.Add(new EbayListingAspect { EbayListingId = listing.Id, Name = name, Value = value });

        await SaveAsync(ct);

        // 2. Act on eBay.
        if (request.Status == EbayListingStatus.Active)
        {
            try
            {
                await _listings.PushAsync(listing, ct);
            }
            catch (EbayApiException ex)
            {
                listing.LastError = EbaySupport.JoinErrors(ex.Errors);
                if (string.IsNullOrEmpty(listing.EbayListingIdExternal))
                    listing.Status = EbayListingStatus.Error;
                _db.AuditTrails.Add(EbaySupport.Audit("EbayListing.PublishFailed", listing.Id, before,
                    new { listing.LastError, Status = listing.Status.ToString() }, _user, _clock));
                await SaveAsync(ct);
                throw new ExternalBusinessException(VerificationErrorTitle, ex.Errors);
            }

            _db.AuditTrails.Add(EbaySupport.Audit(published ? "EbayListing.Revised" : "EbayListing.Published", listing.Id, before,
                new { listing.EbayListingIdExternal, listing.OfferId, listing.Title, listing.Price, listing.Quantity }, _user, _clock));
            await SaveAsync(ct);
        }
        else
        {
            var after = (await EbayListingReader.LoadAsync(_db, _urls, storeId, listing.Id, ct)).ToAuditSnapshot();
            _db.AuditTrails.Add(EbaySupport.Audit("EbayListing.Saved", listing.Id, before, after, _user, _clock));
            await SaveAsync(ct);
        }

        return await EbayListingReader.LoadAsync(_db, _urls, storeId, listing.Id, ct);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException(ConflictMessage); }
    }
}

// ---- End -------------------------------------------------------------------------------------------------

public record EndEbayListingCommand(Guid? StoreId, Guid Id) : IRequest;

public class EndEbayListingHandler : IRequestHandler<EndEbayListingCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayListingService _listings;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public EndEbayListingHandler(IApplicationDbContext db, IEbayListingService listings, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _listings = listings;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(EndEbayListingCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        var listing = await _db.EbayListings.FirstOrDefaultAsync(l => l.Id == request.Id && l.StoreId == storeId, ct)
                      ?? throw new NotFoundException(nameof(EbayListing), request.Id);
        if (listing.Status == EbayListingStatus.Ended)
            return;

        var before = new { Status = listing.Status.ToString(), listing.EbayListingIdExternal, listing.OfferId };
        try
        {
            await _listings.EndAsync(listing, ct);
        }
        catch (EbayApiException ex)
        {
            listing.LastError = EbaySupport.JoinErrors(ex.Errors);
            await _db.SaveChangesAsync(ct);
            throw EbaySupport.BusinessError(ex);
        }

        _db.AuditTrails.Add(EbaySupport.Audit("EbayListing.Ended", listing.Id, before,
            new { Status = listing.Status.ToString(), listing.EndedAtUtc }, _user, _clock));
        await _db.SaveChangesAsync(ct);
    }
}

// ---- Sync now --------------------------------------------------------------------------------------------

public record SyncEbayQuantitiesCommand(Guid? StoreId) : IRequest<EbayInventorySyncResult>;

public class SyncEbayQuantitiesHandler : IRequestHandler<SyncEbayQuantitiesCommand, EbayInventorySyncResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IEbayListingService _listings;
    private readonly IEbayAuthService _auth;

    public SyncEbayQuantitiesHandler(IApplicationDbContext db, IEbayListingService listings, IEbayAuthService auth)
    {
        _db = db;
        _listings = listings;
        _auth = auth;
    }

    public async Task<EbayInventorySyncResult> Handle(SyncEbayQuantitiesCommand request, CancellationToken ct)
    {
        var storeId = await EbaySupport.ResolveStoreAsync(_db, request.StoreId, ct);
        await _auth.GetSessionAsync(storeId, ct: ct); // 503 up front when not connected / disabled
        return await _listings.SyncInventoryAsync(storeId, null, ct);
    }
}
