using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.InventoryScanner;

/// <summary>
/// Save a stock and/or price change from the scanner (PHP InventoryController::updateInventory) and write
/// the audit row — atomically, with an optimistic-concurrency check.
/// <para>
/// <paramref name="ExpectedQty"/> / <paramref name="ExpectedPrice"/> are the values the user SAW. The
/// handler locks the product and its stock rows (UPDLOCK), re-reads them, and either writes the change plus
/// the audit row in one transaction or throws 409 (with <c>currentQty</c> / <c>currentPrice</c>) and writes
/// nothing — two staff scanning the same item can't silently overwrite each other.
/// </para>
/// <para>
/// Stock goes to the primary store's row through <see cref="IInventoryService"/>, so low-stock alerts and
/// back-in-stock notifications fire exactly as for any other stock edit. Price is the product's base price,
/// or the variant's own price when a variant was scanned. <paramref name="NewPrice"/> null = unchanged.
/// </para>
/// </summary>
public record UpdateScannedInventoryCommand(
    Guid ProductId,
    Guid? ProductVariantId,
    int ExpectedQty,
    decimal ExpectedPrice,
    int NewQty,
    decimal? NewPrice = null,
    string? Notes = null,
    string? ScannedCode = null,
    InventoryScanSource Source = InventoryScanSource.Camera,
    InventoryMatchType? MatchedBy = null) : IRequest<InventoryScanLogDto>
{
    // Set by the controller from the authenticated request — never bound from the body.

    /// <summary>Caller holds the Catalog module (price changes); without it a price change is a 403.</summary>
    [JsonIgnore] public bool CanChangePrice { get; init; }
    [JsonIgnore] public string? ClientIp { get; init; }
    [JsonIgnore] public string? UserAgent { get; init; }
}

public class UpdateScannedInventoryCommandValidator : AbstractValidator<UpdateScannedInventoryCommand>
{
    public const int MaxQty = 1_000_000;
    public const decimal MaxPrice = 99_999_999m;

    public UpdateScannedInventoryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.NewQty).InclusiveBetween(0, MaxQty)
            .WithMessage("Stock must be a whole number of 0 or more.");
        RuleFor(x => x.NewPrice!.Value).InclusiveBetween(0m, MaxPrice)
            .WithMessage("Price must be a number of 0 or more.")
            .When(x => x.NewPrice.HasValue);
        RuleFor(x => x.ExpectedPrice).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.MatchedBy).IsInEnum();
        RuleFor(x => x.Notes).MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
        RuleFor(x => x.ScannedCode).MaximumLength(InventoryScannerLookup.MaxCodeLength);
    }
}

public class UpdateScannedInventoryCommandHandler : IRequestHandler<UpdateScannedInventoryCommand, InventoryScanLogDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly ILogger<UpdateScannedInventoryCommandHandler> _logger;

    public UpdateScannedInventoryCommandHandler(IApplicationDbContext db, IInventoryService inventory,
        ILogger<UpdateScannedInventoryCommandHandler> logger)
    {
        _db = db;
        _inventory = inventory;
        _logger = logger;
    }

    public async Task<InventoryScanLogDto> Handle(UpdateScannedInventoryCommand request, CancellationToken cancellationToken)
    {
        // Join an ambient transaction when there is one (tests); otherwise own it.
        IDbContextTransaction? ownTx = _db.Database.CurrentTransaction is null
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await LockRowsAsync(request.ProductId, request.ProductVariantId, cancellationToken);

            // Re-read under the lock: primary-store qty, split/locked flags, effective price.
            var current = await InventoryScannerLookup.BuildAsync(_db, request.ProductId, request.ProductVariantId, cancellationToken)
                ?? throw new NotFoundException(request.ProductVariantId is null ? nameof(Product) : nameof(ProductVariant),
                    (object?)request.ProductVariantId ?? request.ProductId);

            var oldPrice = Math.Round(current.Price ?? 0m, 2);
            if (current.QtyInStock != request.ExpectedQty || oldPrice != Math.Round(request.ExpectedPrice, 2))
            {
                _logger.LogInformation(
                    "Inventory scanner conflict on product {ProductId} variant {VariantId}: expected qty {ExpectedQty} / price {ExpectedPrice}, found {CurrentQty} / {CurrentPrice}",
                    request.ProductId, request.ProductVariantId, request.ExpectedQty, request.ExpectedPrice, current.QtyInStock, oldPrice);
                throw new ConflictException(
                    $"This product was changed by someone else (stock is now {current.QtyInStock}, price {oldPrice:0.00}). Review and try again.",
                    new Dictionary<string, object?>
                    {
                        ["currentQty"] = current.QtyInStock,
                        ["currentPrice"] = oldPrice,
                        ["product"] = current,
                    });
            }

            var newPrice = request.NewPrice is decimal p ? Math.Round(p, 2) : oldPrice;
            var qtyChanged = request.NewQty != current.QtyInStock;
            var priceChanged = newPrice != oldPrice;

            if (!qtyChanged && !priceChanged)
                throw Fail("newQty", "No changes to save: quantity and price are unchanged.");
            if (priceChanged && !request.CanChangePrice)
            {
                _logger.LogWarning("Inventory scanner price change denied on product {ProductId} variant {VariantId}: caller lacks the Catalog module",
                    request.ProductId, request.ProductVariantId);
                throw new ForbiddenAccessException("You don't have permission to change the selling price.");
            }
            if (qtyChanged && !current.CanEditQty)
                throw Fail("newQty", current.QtyLockedReason ?? "The quantity can't be changed from the scanner.");

            if (priceChanged)
            {
                if (request.ProductVariantId is Guid vid)
                    (await _db.ProductVariants.FirstAsync(v => v.Id == vid, cancellationToken)).Price = newPrice;
                else
                    (await _db.Products.FirstAsync(x => x.Id == request.ProductId, cancellationToken)).Price = newPrice;
            }

            var log = new InventoryAuditLog
            {
                ProductId = request.ProductId,
                ProductVariantId = request.ProductVariantId,
                StoreId = qtyChanged ? current.StoreId : null,
                OldQty = current.QtyInStock,
                NewQty = request.NewQty,
                OldPrice = current.Price is null ? null : oldPrice,
                NewPrice = newPrice,
                Action = qtyChanged && priceChanged ? InventoryAuditAction.Both
                    : qtyChanged ? InventoryAuditAction.QtyUpdate : InventoryAuditAction.PriceUpdate,
                Source = request.Source,
                MatchedBy = request.MatchedBy,
                ScannedCode = Clean(request.ScannedCode, InventoryScannerLookup.MaxCodeLength),
                Notes = Clean(request.Notes, 500),
                ClientIp = Clean(request.ClientIp, 45),
                UserAgent = Clean(request.UserAgent, 300),
            };
            _db.InventoryAuditLogs.Add(log);

            if (qtyChanged)
            {
                // Saves the price + log too (same unit of work), then runs the low-stock / replenished checks.
                await _inventory.SetStockLevelAsync(request.ProductId, request.ProductVariantId, current.StoreId!.Value,
                    request.NewQty, null, cancellationToken);
            }
            else
            {
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (ownTx is not null)
                await ownTx.CommitAsync(cancellationToken);

            return (await InventoryScannerLookup.GetLogAsync(_db, log.Id, cancellationToken))!;
        }
        finally
        {
            // Disposing an uncommitted transaction rolls it back: a 400/403/404/409 writes nothing.
            if (ownTx is not null)
                await ownTx.DisposeAsync();
        }
    }

    /// <summary>
    /// Takes update locks, held to the end of the transaction, on the product (or variant) row — which
    /// serializes concurrent scanner saves of the same item — and on its stock rows. HOLDLOCK also covers
    /// the key range, so a first-ever stock row can't be inserted underneath us.
    /// </summary>
    private async Task LockRowsAsync(Guid productId, Guid? variantId, CancellationToken ct)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT [Id] FROM [Products] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {productId}", ct);

        if (variantId is Guid vid)
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT [Id] FROM [ProductVariants] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {vid}", ct);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT [Id] FROM [InventoryLevels] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {productId} AND [ProductVariantId] = {vid}", ct);
        }
        else
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT [Id] FROM [InventoryLevels] WITH (UPDLOCK, HOLDLOCK) WHERE [ProductId] = {productId} AND [ProductVariantId] IS NULL", ct);
        }
    }

    private static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length <= max ? v : v[..max];
    }

    private static ValidationException Fail(string field, string message) =>
        new(new[] { new ValidationFailure(field, message) });
}
