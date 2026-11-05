using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.GiftCertificates;

/// <summary>The certificate cannot be used (any reason) — always the same message, never revealing whether a code exists.</summary>
public class GiftCertificateUnavailableException : ExternalBusinessException
{
    public GiftCertificateUnavailableException(string message)
        : base(message, new[] { new ExternalError("gift_certificate", message) }) { }
}

public sealed record GiftCertificateCreateRequest(
    Guid StoreId,
    decimal Amount,
    GiftCertificateStatus Status,
    GiftCertificateSource Source,
    bool IsOnline,
    string? SenderName,
    string? SenderEmail,
    string? RecipientName,
    string? RecipientEmail,
    string? Message,
    DateTime? ExpiresAtUtc,
    string? Comment,
    Guid? SourceOrderId = null,
    Guid? SourceOrderItemId = null,
    int? SourceUnitIndex = null);

public static class GiftCertificateRules
{
    public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public const int MinCodeLength = 10;
    public const decimal MaxAmount = 10_000m;
    public const string InvalidMessage = "Invalid Gift Certificate";
    public const string NoLongerValidMessage = "The gift certificate you have submitted is no longer valid";
    public const string GcForGcMessage = "Gift certificates cannot be used to purchase gift certificates";
    public const string PaymentMethodName = "Gift Certificate";

    // Store settings (global key/value today; see ISettingsService).
    public const string CodePrefixKey = "GiftCertificate.CodePrefix";
    public const string CodeLengthKey = "GiftCertificate.CodeLength";
    public const string AllowRedeemOnGcPurchaseKey = "GiftCertificate.AllowRedeemOnGcPurchase";
    public const string DefaultExpiryMonthsKey = "GiftCertificate.DefaultExpiryMonths";
    /// <summary>CSV of PaymentMethodType names allowed when the cart holds a certificate product (AllowGiftCertificatePurchase).</summary>
    public const string PurchasePaymentMethodsKey = "GiftCertificate.PurchasePaymentMethods";
    public const string DefaultPurchasePaymentMethods = "Stripe,PayPal,Razorpay,Square,AuthorizeNet";

    /// <summary>The one apply formula used by the cart, checkout summary, payment and order completion.</summary>
    public static decimal ApplyAmount(decimal currentBalance, decimal grandTotal) =>
        Math.Round(Math.Max(0m, Math.Min(currentBalance, grandTotal)), 2, MidpointRounding.AwayFromZero);

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>"****WXYZ" — never log a full code.</summary>
    public static string Mask(string? code) =>
        string.IsNullOrEmpty(code) ? "****" : "****" + (code.Length <= 4 ? code : code[^4..]);

    public static bool IsRedeemable(GiftCertificate gc, DateTime nowUtc) =>
        gc.Status == GiftCertificateStatus.Active && !gc.Deleted && gc.CurrentBalance > 0m
        && (gc.ExpiresAtUtc is null || gc.ExpiresAtUtc > nowUtc);
}

/// <summary>
/// Every gift-certificate balance change goes through <see cref="ApplyBalanceChangeAsync"/>: one guarded,
/// atomic UPDATE plus the ledger row, in one transaction. Issuing, redeeming, refunding and voiding are built
/// on it, so CurrentBalance = OriginalBalance + Σ(ledger after Created) always holds.
/// </summary>
public interface IGiftCertificateService
{
    Task<string> GenerateCodeAsync(Guid storeId, CancellationToken ct = default);
    Task<GiftCertificate> CreateAsync(GiftCertificateCreateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Adds <paramref name="delta"/> (signed) to the balance and writes the ledger entry. A debit requires the
    /// balance to cover it; with <paramref name="requireRedeemable"/> the certificate must also be active, not
    /// deleted and not expired. Throws <see cref="GiftCertificateUnavailableException"/> when the guard fails.
    /// Joins the caller's transaction when one is open (so the caller's pending changes commit with it).
    /// </summary>
    Task<GiftCertificateTransaction> ApplyBalanceChangeAsync(
        Guid giftCertificateId, decimal delta, GiftCertificateAction action, Guid? orderId, string? comment,
        bool requireRedeemable = false, CancellationToken ct = default);

    /// <summary>Records a status change (ledger amount 0).</summary>
    Task RecordStatusChangeAsync(Guid giftCertificateId, string? comment, CancellationToken ct = default);

    /// <summary>The redeemable certificate for a code in the store, or null (trimmed, case-insensitive).</summary>
    Task<GiftCertificate?> FindRedeemableAsync(Guid storeId, string? code, CancellationToken ct = default);

    /// <summary>
    /// Deducts the order's applied amount and records the gift-certificate payment (+ ledger). Idempotent
    /// (an already-redeemed order is a no-op). Throws <see cref="GiftCertificateUnavailableException"/>
    /// ("no longer valid") when the certificate can no longer cover it.
    /// </summary>
    Task RedeemForOrderAsync(Order order, bool coversFullAmount, CancellationToken ct = default);

    /// <summary>Issues one certificate per purchased unit of a paid order (idempotent). Returns the new ones.</summary>
    Task<IReadOnlyList<GiftCertificate>> IssueForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>
    /// Order cancelled: restores whatever the order still holds from its redeemed certificate, and voids the
    /// certificates it purchased (unused only; a partly used one flags the order for manual review).
    /// </summary>
    Task HandleOrderCancelledAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>Voids the certificates an order purchased (unused only; a partly used one flags the order for review).</summary>
    Task VoidPurchasedAsync(Guid orderId, string why, CancellationToken ct = default);

    /// <summary>Restores a refunded portion of a gift-certificate payment back to the certificate.</summary>
    Task RestoreForRefundAsync(PaymentRecord giftCertificatePayment, decimal amount, string? reason, CancellationToken ct = default);

    /// <summary>Soft-deletes (or just voids) a certificate, writing a Voided entry for the remaining balance.</summary>
    Task VoidAsync(Guid giftCertificateId, string? comment, bool softDelete, CancellationToken ct = default);
}

public sealed class GiftCertificateService : IGiftCertificateService
{
    private const int MaxCodeAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;
    private readonly IGiftCertificateMailer _mailer;
    private readonly IAdminAlertService _alerts;
    private readonly ILogger<GiftCertificateService> _logger;

    public GiftCertificateService(
        IApplicationDbContext db, ISettingsService settings, ICurrentUserService user, IDateTimeProvider clock,
        IGiftCertificateMailer mailer, IAdminAlertService alerts, ILogger<GiftCertificateService> logger)
    {
        _db = db;
        _settings = settings;
        _user = user;
        _clock = clock;
        _mailer = mailer;
        _alerts = alerts;
        _logger = logger;
    }

    private string Actor => _user.Email ?? (_user.UserId?.ToString() ?? "system");

    // ---- Codes --------------------------------------------------------------------------------------

    public async Task<string> GenerateCodeAsync(Guid storeId, CancellationToken ct = default)
    {
        var prefix = GiftCertificateRules.Normalize(await _settings.GetValueAsync(GiftCertificateRules.CodePrefixKey, ct));
        var length = Math.Max(GiftCertificateRules.MinCodeLength,
            int.TryParse(await _settings.GetValueAsync(GiftCertificateRules.CodeLengthKey, ct), out var l) ? l : 12);
        length = Math.Min(length, 50 - prefix.Length);

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = prefix + RandomCode(length);
            if (!await _db.GiftCertificates.IgnoreQueryFilters().AnyAsync(g => g.StoreId == storeId && g.Code == code, ct))
                return code;
        }

        throw new ConflictException("Could not generate a unique gift certificate code. Try again.");
    }

    public static string RandomCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
            chars[i] = GiftCertificateRules.Alphabet[RandomNumberGenerator.GetInt32(GiftCertificateRules.Alphabet.Length)];
        return new string(chars);
    }

    public async Task<GiftCertificate> CreateAsync(GiftCertificateCreateRequest request, CancellationToken ct = default)
    {
        var amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero);
        var now = _clock.UtcNow;

        // The unique (StoreId, Code) index is the real guard; a collision between the existence check and
        // the insert retries with a fresh code (up to 5 attempts).
        for (var attempt = 1; ; attempt++)
        {
            var gc = new GiftCertificate
            {
                StoreId = request.StoreId,
                Code = await GenerateCodeAsync(request.StoreId, ct),
                OriginalBalance = amount,
                CurrentBalance = amount,
                Status = request.Status,
                Source = request.Source,
                IsOnline = request.IsOnline,
                SenderName = Trim(request.SenderName, 75),
                SenderEmail = Trim(request.SenderEmail, 75),
                RecipientName = Trim(request.RecipientName, 75),
                RecipientEmail = Trim(request.RecipientEmail, 75),
                Message = Trim(request.Message, 1000),
                ExpiresAtUtc = request.ExpiresAtUtc,
                SourceOrderId = request.SourceOrderId,
                SourceOrderItemId = request.SourceOrderItemId,
                SourceUnitIndex = request.SourceUnitIndex,
            };
            gc.Transactions.Add(new GiftCertificateTransaction
            {
                Action = GiftCertificateAction.Created,
                Amount = amount,
                BalanceAfter = amount,
                OrderId = request.SourceOrderId,
                Comment = Trim(request.Comment, 255),
                CreatedOnUtc = now,
                CreatedBy = request.Source == GiftCertificateSource.Purchased ? "system" : Actor,
            });
            _db.GiftCertificates.Add(gc);

            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Gift certificate {Code} created ({Source}, {Amount}).",
                    GiftCertificateRules.Mask(gc.Code), gc.Source, amount);
                return gc;
            }
            catch (DbUpdateException) when (attempt < MaxCodeAttempts && request.SourceOrderItemId is null)
            {
                Detach(gc);
            }
        }
    }

    // ---- The one balance-change method --------------------------------------------------------------

    public async Task<GiftCertificateTransaction> ApplyBalanceChangeAsync(
        Guid giftCertificateId, decimal delta, GiftCertificateAction action, Guid? orderId, string? comment,
        bool requireRedeemable = false, CancellationToken ct = default)
    {
        delta = Math.Round(delta, 2, MidpointRounding.AwayFromZero);
        var now = _clock.UtcNow;

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var query = _db.GiftCertificates.IgnoreQueryFilters().Where(g => g.Id == giftCertificateId);
            if (delta < 0)
            {
                var debit = -delta;
                query = query.Where(g => g.CurrentBalance >= debit);
            }

            if (requireRedeemable)
                query = query.Where(g => g.Status == GiftCertificateStatus.Active && !g.Deleted
                                         && (g.ExpiresAtUtc == null || g.ExpiresAtUtc > now));

            // Atomic: the guard and the change are one statement, so two redemptions racing for the same
            // balance cannot both succeed. The row stays locked until this transaction ends.
            var affected = await query.ExecuteUpdateAsync(s => s.SetProperty(g => g.CurrentBalance, g => g.CurrentBalance + delta), ct);
            if (affected == 0)
                throw new GiftCertificateUnavailableException(GiftCertificateRules.NoLongerValidMessage);

            var balanceAfter = await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.Id == giftCertificateId).Select(g => g.CurrentBalance).FirstAsync(ct);

            var entry = new GiftCertificateTransaction
            {
                GiftCertificateId = giftCertificateId,
                Action = action,
                Amount = delta,
                BalanceAfter = balanceAfter,
                OrderId = orderId,
                Comment = Trim(comment, 255),
                CreatedOnUtc = now,
                CreatedBy = Actor,
            };
            _db.GiftCertificateTransactions.Add(entry);
            await _db.SaveChangesAsync(ct);

            if (tx is not null) await tx.CommitAsync(ct);
            return entry;
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    public async Task RecordStatusChangeAsync(Guid giftCertificateId, string? comment, CancellationToken ct = default)
    {
        var balance = await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
            .Where(g => g.Id == giftCertificateId).Select(g => g.CurrentBalance).FirstAsync(ct);
        _db.GiftCertificateTransactions.Add(new GiftCertificateTransaction
        {
            GiftCertificateId = giftCertificateId,
            Action = GiftCertificateAction.StatusChanged,
            Amount = 0m,
            BalanceAfter = balance,
            Comment = Trim(comment, 255),
            CreatedOnUtc = _clock.UtcNow,
            CreatedBy = Actor,
        });
        await _db.SaveChangesAsync(ct);
    }

    // ---- Lookup -------------------------------------------------------------------------------------

    public async Task<GiftCertificate?> FindRedeemableAsync(Guid storeId, string? code, CancellationToken ct = default)
    {
        var normalized = GiftCertificateRules.Normalize(code);
        if (normalized.Length == 0 || normalized.Length > 50)
            return null;

        var gc = await _db.GiftCertificates.AsNoTracking()
            .FirstOrDefaultAsync(g => g.StoreId == storeId && g.Code == normalized, ct);
        return gc is not null && GiftCertificateRules.IsRedeemable(gc, _clock.UtcNow) ? gc : null;
    }

    // ---- Redeem -------------------------------------------------------------------------------------

    public async Task RedeemForOrderAsync(Order order, bool coversFullAmount, CancellationToken ct = default)
    {
        if (order.GiftCertificateId is not Guid gcId || order.GiftCertificateAppliedAmount <= 0m)
            return;

        var alreadyRedeemed = await _db.PaymentRecords.AsNoTracking().AnyAsync(
            p => p.OrderId == order.Id && p.Method == PaymentMethodType.GiftCertificate
                 && p.Status != PaymentStatus.Failed && p.Status != PaymentStatus.Voided, ct);
        if (alreadyRedeemed)
            return;

        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var now = _clock.UtcNow;
            _db.PaymentRecords.Add(new PaymentRecord
            {
                OrderId = order.Id,
                Method = PaymentMethodType.GiftCertificate,
                GatewayName = GiftCertificateRules.PaymentMethodName,
                Amount = order.GiftCertificateAppliedAmount,
                CurrencyCode = order.CurrencyCode,
                Status = PaymentStatus.Captured,
                TransactionId = order.GiftCertificateCode,
                GatewayReference = order.GiftCertificateCode,
                AuthorizedOnUtc = now,
                CapturedOnUtc = now,
                IsMaster = coversFullAmount,
            });

            // Flushes the payment row together with the debit + ledger, in this one transaction.
            await ApplyBalanceChangeAsync(gcId, -order.GiftCertificateAppliedAmount, GiftCertificateAction.Redeemed,
                order.Id, $"Order #{order.OrderNumber}", requireRedeemable: true, ct);

            if (tx is not null) await tx.CommitAsync(ct);
            _logger.LogInformation("Gift certificate {Code} redeemed {Amount} on order {OrderNumber}.",
                GiftCertificateRules.Mask(order.GiftCertificateCode), order.GiftCertificateAppliedAmount, order.OrderNumber);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            DetachAddedPayments(order.Id);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    // ---- Issue --------------------------------------------------------------------------------------

    public async Task<IReadOnlyList<GiftCertificate>> IssueForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null || order.PaymentStatus != PaymentStatus.Captured)
            return Array.Empty<GiftCertificate>();

        var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var gcProducts = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.ProductType == ProductType.GiftCard)
            .ToDictionaryAsync(p => p.Id, p => p.GiftCertificateOnline, ct);
        if (gcProducts.Count == 0)
            return Array.Empty<GiftCertificate>();

        var lineIds = order.Lines.Select(l => l.Id).ToList();
        var existing = (await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.SourceOrderItemId != null && lineIds.Contains(g.SourceOrderItemId.Value))
                .Select(g => new { g.SourceOrderItemId, g.SourceUnitIndex })
                .ToListAsync(ct))
            .Select(x => (x.SourceOrderItemId!.Value, x.SourceUnitIndex ?? 0))
            .ToHashSet();

        var storeId = order.AssignedStoreId ?? await StoreScope.ResolveAsync(_db, null, ct);
        DateTime? expires = int.TryParse(await _settings.GetValueAsync(GiftCertificateRules.DefaultExpiryMonthsKey, ct),
            NumberStyles.Integer, CultureInfo.InvariantCulture, out var months) && months > 0
            ? _clock.UtcNow.AddMonths(months)
            : null;

        var issued = new List<GiftCertificate>();
        foreach (var line in order.Lines.Where(l => gcProducts.ContainsKey(l.ProductId)))
        {
            for (var unit = 0; unit < line.Quantity; unit++)
            {
                if (existing.Contains((line.Id, unit)))
                    continue;

                try
                {
                    issued.Add(await CreateAsync(new GiftCertificateCreateRequest(
                        storeId, line.UnitPrice, GiftCertificateStatus.Active, GiftCertificateSource.Purchased,
                        gcProducts[line.ProductId], line.GcSenderName, line.GcSenderEmail, line.GcRecipientName,
                        line.GcRecipientEmail, line.GcMessage, expires, $"Purchased with order #{order.OrderNumber}",
                        order.Id, line.Id, unit), ct));
                }
                catch (DbUpdateException)
                {
                    // Another run issued this unit concurrently — the unique (line, unit) index kept it single.
                    ClearPendingCertificates();
                }
            }
        }

        foreach (var gc in issued.Where(g => g.IsOnline && !string.IsNullOrWhiteSpace(g.RecipientEmail)))
        {
            try { await _mailer.QueueAsync(gc.Id, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not queue the email for gift certificate {Code}.", GiftCertificateRules.Mask(gc.Code));
            }
        }

        return issued;
    }

    // ---- Cancel / refund / void ---------------------------------------------------------------------

    public async Task HandleOrderCancelledAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is null)
            return;

        // 1. Give back what the order still holds from its redeemed certificate (even if since deactivated).
        var gcPayments = await _db.PaymentRecords
            .Where(p => p.OrderId == orderId && p.Method == PaymentMethodType.GiftCertificate
                        && (p.Status == PaymentStatus.Captured || p.Status == PaymentStatus.PartiallyRefunded))
            .ToListAsync(ct);
        foreach (var payment in gcPayments)
        {
            var remaining = payment.Amount - payment.RefundedAmount;
            if (remaining > 0m)
                await RestoreForRefundAsync(payment, remaining, $"Order #{order.OrderNumber} cancelled", ct);
        }

        // 2. Void the certificates this order purchased — but never one that has already been spent from.
        await VoidPurchasedAsync(order, "cancelled", ct);
    }

    public async Task RestoreForRefundAsync(PaymentRecord payment, decimal amount, string? reason, CancellationToken ct = default)
    {
        if (payment.Method != PaymentMethodType.GiftCertificate || amount <= 0m)
            return;

        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == payment.OrderId, ct);
        if (order?.GiftCertificateId is not Guid gcId)
            return;

        amount = Math.Min(Math.Round(amount, 2, MidpointRounding.AwayFromZero), payment.Amount - payment.RefundedAmount);
        if (amount <= 0m)
            return;

        payment.RefundedAmount += amount;
        payment.RefundedOnUtc = _clock.UtcNow;
        payment.Status = payment.RefundedAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;

        // The payment update is flushed with the credit + ledger, atomically.
        await ApplyBalanceChangeAsync(gcId, amount, GiftCertificateAction.Refunded, order.Id,
            string.IsNullOrWhiteSpace(reason) ? $"Refund on order #{order.OrderNumber}" : reason, requireRedeemable: false, ct);
    }

    /// <summary>Voids the certificates an order purchased (used when the purchase is cancelled or fully refunded).</summary>
    public async Task VoidPurchasedAsync(Guid orderId, string why, CancellationToken ct = default)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order is not null)
            await VoidPurchasedAsync(order, why, ct);
    }

    private async Task VoidPurchasedAsync(Order order, string why, CancellationToken ct)
    {
        // No tracking: balances are changed with ExecuteUpdate, so a tracked instance could be stale.
        var purchased = await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
            .Where(g => g.SourceOrderId == order.Id && !g.Deleted && g.Status == GiftCertificateStatus.Active)
            .ToListAsync(ct);

        var partlyUsed = new List<string>();
        foreach (var gc in purchased)
        {
            if (gc.CurrentBalance < gc.OriginalBalance)
            {
                partlyUsed.Add(GiftCertificateRules.Mask(gc.Code));
                continue;
            }

            await VoidAsync(gc.Id, $"Purchase order #{order.OrderNumber} {why}", softDelete: false, ct);
        }

        if (partlyUsed.Count > 0)
        {
            var note = $"[Gift certificates] Order {why}, but certificate(s) {string.Join(", ", partlyUsed)} were already partly used — review manually.";
            var tracked = await _db.Orders.FirstAsync(o => o.Id == order.Id, ct);
            tracked.InternalNotes = string.IsNullOrWhiteSpace(tracked.InternalNotes) ? note : tracked.InternalNotes + Environment.NewLine + note;
            await _db.SaveChangesAsync(ct);
            await _alerts.RaiseAsync("GiftCertificateReview", $"Order {order.OrderNumber} needs a gift certificate review", note, "Warning", "GiftCertificates", ct);
        }
    }

    public async Task VoidAsync(Guid giftCertificateId, string? comment, bool softDelete, CancellationToken ct = default)
    {
        var ownsTransaction = _db.Database.CurrentTransaction is null;
        IDbContextTransaction? tx = ownsTransaction ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var now = _clock.UtcNow;
            await _db.GiftCertificates.IgnoreQueryFilters().Where(g => g.Id == giftCertificateId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.Status, GiftCertificateStatus.Inactive)
                    .SetProperty(g => g.Deleted, g => softDelete || g.Deleted)
                    .SetProperty(g => g.DeletedOnUtc, g => softDelete ? now : g.DeletedOnUtc)
                    .SetProperty(g => g.UpdatedOnUtc, now), ct);

            var balance = await _db.GiftCertificates.IgnoreQueryFilters().AsNoTracking()
                .Where(g => g.Id == giftCertificateId).Select(g => g.CurrentBalance).FirstAsync(ct);

            if (balance > 0m)
            {
                await ApplyBalanceChangeAsync(giftCertificateId, -balance, GiftCertificateAction.Voided, null, comment, false, ct);
            }
            else
            {
                _db.GiftCertificateTransactions.Add(new GiftCertificateTransaction
                {
                    GiftCertificateId = giftCertificateId,
                    Action = GiftCertificateAction.Voided,
                    Amount = 0m,
                    BalanceAfter = 0m,
                    Comment = Trim(comment, 255),
                    CreatedOnUtc = now,
                    CreatedBy = Actor,
                });
                await _db.SaveChangesAsync(ct);
            }

            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max];
    }

    private void Detach(GiftCertificate gc)
    {
        if (_db is DbContext context)
        {
            foreach (var t in gc.Transactions) context.Entry(t).State = EntityState.Detached;
            context.Entry(gc).State = EntityState.Detached;
        }
    }

    private void ClearPendingCertificates()
    {
        if (_db is not DbContext context) return;
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added && e.Entity is GiftCertificate or GiftCertificateTransaction)
                     .ToList())
            entry.State = EntityState.Detached;
    }

    private void DetachAddedPayments(Guid orderId)
    {
        if (_db is not DbContext context) return;
        foreach (var entry in context.ChangeTracker.Entries<PaymentRecord>()
                     .Where(e => e.State == EntityState.Added && e.Entity.OrderId == orderId
                                 && e.Entity.Method == PaymentMethodType.GiftCertificate)
                     .ToList())
            entry.State = EntityState.Detached;
        foreach (var entry in context.ChangeTracker.Entries<GiftCertificateTransaction>()
                     .Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }
}

/// <summary>Queues the gift-certificate email (recipient, CC + Reply-To the sender, from the store address).</summary>
public interface IGiftCertificateMailer
{
    /// <returns>False when there is no recipient email or the certificate is not found.</returns>
    Task<bool> QueueAsync(Guid giftCertificateId, CancellationToken ct = default);
}
