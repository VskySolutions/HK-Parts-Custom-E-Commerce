using System.Globalization;
using System.Text;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.GiftCertificates;

// ---- List / export -------------------------------------------------------------------------------------

public record ListGiftCertificatesQuery(
    Guid? StoreId = null,
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    GiftCertificateStatus? Status = null,
    GiftCertificateSource? Source = null,
    bool HasBalance = false,
    string? Sort = null,
    string? Dir = null) : IRequest<PaginatedList<GiftCertificateListItemDto>>;

internal static class GiftCertificateListQuery
{
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["code"] = nameof(GiftCertificate.Code),
        ["createdOnUtc"] = nameof(GiftCertificate.CreatedOnUtc),
        ["amount"] = nameof(GiftCertificate.OriginalBalance),
        ["balance"] = nameof(GiftCertificate.CurrentBalance),
        ["status"] = nameof(GiftCertificate.Status),
        ["recipientName"] = nameof(GiftCertificate.RecipientName),
    };

    public static async Task<IQueryable<GiftCertificateListItemDto>> BuildAsync(
        IApplicationDbContext db, ListGiftCertificatesQuery request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(db, request.StoreId, ct);
        var query = db.GiftCertificates.AsNoTracking().Where(g => g.StoreId == storeId);

        if (request.Status is GiftCertificateStatus status) query = query.Where(g => g.Status == status);
        if (request.Source is GiftCertificateSource source) query = query.Where(g => g.Source == source);
        if (request.HasBalance) query = query.Where(g => g.CurrentBalance > 0);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var code = term.ToUpperInvariant();
            // Purchaser = the purchasing (source) order's contact name.
            query = query.Where(g =>
                g.Code.Contains(code)
                || (g.RecipientEmail != null && g.RecipientEmail.Contains(term))
                || (g.RecipientName != null && g.RecipientName.Contains(term))
                || (g.SourceOrder != null && g.SourceOrder.ShippingAddress != null
                    && ((g.SourceOrder.ShippingAddress.FirstName ?? "") + " " + (g.SourceOrder.ShippingAddress.LastName ?? "")).Contains(term)));
        }

        var descending = !string.Equals(request.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        var ordered = string.IsNullOrWhiteSpace(request.Sort)
            ? query.OrderByDescending(g => g.CreatedOnUtc)
            : query.ApplySort(request.Sort, descending, SortMap, defaultSort: q => q.OrderByDescending(g => g.CreatedOnUtc));

        return ordered.Select(g => new GiftCertificateListItemDto
        {
            Id = g.Id,
            Code = g.Code,
            CreatedOnUtc = g.CreatedOnUtc,
            Source = g.Source,
            SourceOrderId = g.SourceOrderId,
            SourceOrderNumber = g.SourceOrder != null ? g.SourceOrder.OrderNumber : null,
            PurchasedBy = g.SourceOrder != null && g.SourceOrder.ShippingAddress != null
                ? ((g.SourceOrder.ShippingAddress.FirstName ?? "") + " " + (g.SourceOrder.ShippingAddress.LastName ?? "")).Trim()
                : null,
            RecipientName = g.RecipientName,
            RecipientEmail = g.RecipientEmail,
            Amount = g.OriginalBalance,
            Spent = g.OriginalBalance - g.CurrentBalance,
            Balance = g.CurrentBalance,
            Status = g.Status,
            IsOnline = g.IsOnline,
            ExpiresAtUtc = g.ExpiresAtUtc,
        });
    }
}

public class ListGiftCertificatesHandler : IRequestHandler<ListGiftCertificatesQuery, PaginatedList<GiftCertificateListItemDto>>
{
    private readonly IApplicationDbContext _db;

    public ListGiftCertificatesHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<GiftCertificateListItemDto>> Handle(ListGiftCertificatesQuery request, CancellationToken ct)
    {
        var query = await GiftCertificateListQuery.BuildAsync(_db, request, ct);
        return await PaginatedList<GiftCertificateListItemDto>.CreateAsync(query, request.Page, Math.Clamp(request.PageSize, 1, 200), ct);
    }
}

public record ExportGiftCertificatesQuery(ListGiftCertificatesQuery Filter) : IRequest<byte[]>;

public class ExportGiftCertificatesHandler : IRequestHandler<ExportGiftCertificatesQuery, byte[]>
{
    private readonly IApplicationDbContext _db;

    public ExportGiftCertificatesHandler(IApplicationDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportGiftCertificatesQuery request, CancellationToken ct)
    {
        var rows = await (await GiftCertificateListQuery.BuildAsync(_db, request.Filter, ct)).Take(50_000).ToListAsync(ct);
        var csv = new StringBuilder("Code,Created (UTC),Source,Source Order,Purchased By,Recipient Name,Recipient Email,Amount,Spent,Balance,Status,Online,Expires (UTC)\r\n");
        foreach (var r in rows)
        {
            csv.Append(string.Join(',', new[]
            {
                Cell(r.Code), Cell(r.CreatedOnUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)), Cell(r.Source.ToString()),
                Cell(r.SourceOrderNumber), Cell(r.PurchasedBy), Cell(r.RecipientName), Cell(r.RecipientEmail),
                Money(r.Amount), Money(r.Spent), Money(r.Balance), Cell(r.Status.ToString()), r.IsOnline ? "Yes" : "No",
                Cell(r.ExpiresAtUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            })).Append("\r\n");
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    private static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Quotes a cell and neutralises spreadsheet formula injection.</summary>
    private static string Cell(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}

// ---- Get -----------------------------------------------------------------------------------------------

public record GetGiftCertificateQuery(Guid? StoreId, Guid Id) : IRequest<GiftCertificateDto>;

public class GetGiftCertificateHandler : IRequestHandler<GetGiftCertificateQuery, GiftCertificateDto>
{
    private readonly IApplicationDbContext _db;

    public GetGiftCertificateHandler(IApplicationDbContext db) => _db = db;

    public async Task<GiftCertificateDto> Handle(GetGiftCertificateQuery request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(_db, request.StoreId, ct);
        return await GiftCertificateReader.LoadAsync(_db, storeId, request.Id, ct);
    }
}

public static class GiftCertificateReader
{
    public static async Task<GiftCertificateDto> LoadAsync(IApplicationDbContext db, Guid storeId, Guid id, CancellationToken ct)
    {
        var gc = await db.GiftCertificates.AsNoTracking()
            .Include(g => g.SourceOrder).ThenInclude(o => o!.ShippingAddress)
            .FirstOrDefaultAsync(g => g.Id == id && g.StoreId == storeId, ct)
            ?? throw new NotFoundException(nameof(GiftCertificate), id);

        var ledger = await (
            from t in db.GiftCertificateTransactions.AsNoTracking()
            join o in db.Orders.IgnoreQueryFilters().AsNoTracking() on t.OrderId equals (Guid?)o.Id into oj
            from o in oj.DefaultIfEmpty()
            where t.GiftCertificateId == id
            orderby t.CreatedOnUtc descending
            select new GiftCertificateLedgerDto
            {
                Id = t.Id,
                CreatedOnUtc = t.CreatedOnUtc,
                Action = t.Action,
                Amount = t.Amount,
                BalanceAfter = t.BalanceAfter,
                OrderId = t.OrderId,
                OrderNumber = o != null ? o.OrderNumber : null,
                Comment = t.Comment,
                CreatedBy = t.CreatedBy,
            }).ToListAsync(ct);

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.GiftCertificateId == id)
            .OrderByDescending(o => o.PlacedOnUtc)
            .Select(o => new GiftCertificateOrderDto
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber,
                PlacedOnUtc = o.PlacedOnUtc,
                BillTo = o.ShippingAddress == null ? null : ((o.ShippingAddress.FirstName ?? "") + " " + (o.ShippingAddress.LastName ?? "")).Trim(),
                ShipTo = o.ShippingAddress == null ? null : o.ShippingAddress.AddressLine1 + ", " + o.ShippingAddress.City + " " + o.ShippingAddress.PostalCode,
                OrderTotal = o.TotalAmount,
                AppliedAmount = o.GiftCertificateAppliedAmount,
                Paid = o.PaymentStatus == PaymentStatus.Captured,
                Status = o.Status.ToString(),
                EbayOrderId = o.EbayOrderId,
            })
            .ToListAsync(ct);

        var buyer = gc.SourceOrder?.ShippingAddress;
        return new GiftCertificateDto
        {
            Id = gc.Id,
            StoreId = gc.StoreId,
            Code = gc.Code,
            Source = gc.Source,
            SourceOrderId = gc.SourceOrderId,
            SourceOrderNumber = gc.SourceOrder?.OrderNumber,
            PurchasedBy = buyer is null ? null : $"{buyer.FirstName} {buyer.LastName}".Trim(),
            OriginalBalance = gc.OriginalBalance,
            CurrentBalance = gc.CurrentBalance,
            Status = gc.Status,
            IsOnline = gc.IsOnline,
            SenderName = gc.SenderName,
            SenderEmail = gc.SenderEmail,
            RecipientName = gc.RecipientName,
            RecipientEmail = gc.RecipientEmail,
            Message = gc.Message,
            EmailSentAtUtc = gc.EmailSentAtUtc,
            ExpiresAtUtc = gc.ExpiresAtUtc,
            CreatedOnUtc = gc.CreatedOnUtc,
            UpdatedOnUtc = gc.UpdatedOnUtc,
            RowVersion = Convert.ToBase64String(gc.RowVersion ?? Array.Empty<byte>()),
            Ledger = ledger,
            Orders = orders,
        };
    }
}

// ---- Create --------------------------------------------------------------------------------------------

public record CreateGiftCertificateCommand(
    Guid? StoreId,
    decimal Amount,
    GiftCertificateStatus Status,
    string? RecipientName,
    string? RecipientEmail,
    string? Message,
    DateTime? ExpiresAt,
    bool SendEmail,
    string? Comment) : IRequest<GiftCertificateDto>;

public class CreateGiftCertificateValidator : AbstractValidator<CreateGiftCertificateCommand>
{
    public CreateGiftCertificateValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Amount must be greater than 0.")
            .LessThanOrEqualTo(GiftCertificateRules.MaxAmount).WithMessage("Amount cannot exceed 10,000.")
            .Must(a => decimal.Round(a, 2) == a).WithMessage("Amount can have at most 2 decimals.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.RecipientName).MaximumLength(75);
        RuleFor(x => x.RecipientEmail).MaximumLength(75).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.RecipientEmail));
        RuleFor(x => x.RecipientEmail).NotEmpty().When(x => x.SendEmail).WithMessage("A recipient email is required to send the certificate.");
        RuleFor(x => x.Message).MaximumLength(1000);
        RuleFor(x => x.Comment).MaximumLength(255);
    }
}

public class CreateGiftCertificateHandler : IRequestHandler<CreateGiftCertificateCommand, GiftCertificateDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IGiftCertificateService _service;
    private readonly IGiftCertificateMailer _mailer;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public CreateGiftCertificateHandler(
        IApplicationDbContext db, IGiftCertificateService service, IGiftCertificateMailer mailer,
        ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _service = service;
        _mailer = mailer;
        _user = user;
        _clock = clock;
    }

    public async Task<GiftCertificateDto> Handle(CreateGiftCertificateCommand request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(_db, request.StoreId, ct);
        var storeName = await _db.Stores.AsNoTracking().Where(s => s.Id == storeId).Select(s => s.Name).FirstAsync(ct);

        var gc = await _service.CreateAsync(new GiftCertificateCreateRequest(
            storeId, request.Amount, request.Status, GiftCertificateSource.Admin, IsOnline: true,
            SenderName: storeName, SenderEmail: null, request.RecipientName, request.RecipientEmail, request.Message,
            request.ExpiresAt, request.Comment), ct);

        _db.AuditTrails.Add(GiftCertificateAudit.Entry("GiftCertificate.Created", gc.Id, null,
            new { gc.OriginalBalance, Status = gc.Status.ToString(), gc.RecipientEmail, gc.ExpiresAtUtc }, _user, _clock));
        await _db.SaveChangesAsync(ct);

        if (request.SendEmail)
            await _mailer.QueueAsync(gc.Id, ct);

        return await GiftCertificateReader.LoadAsync(_db, storeId, gc.Id, ct);
    }
}

// ---- Update --------------------------------------------------------------------------------------------

public record UpdateGiftCertificateCommand(
    Guid? StoreId,
    Guid Id,
    GiftCertificateStatus Status,
    DateTime? ExpiresAt,
    string? RecipientName,
    string? RecipientEmail,
    string? Message,
    decimal? NewBalance,
    decimal? AdjustBy,
    string? Comment,
    string? RowVersion) : IRequest<GiftCertificateDto>;

public class UpdateGiftCertificateValidator : AbstractValidator<UpdateGiftCertificateCommand>
{
    public UpdateGiftCertificateValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.NewBalance).GreaterThanOrEqualTo(0).WithMessage("The balance cannot be negative.")
            .LessThanOrEqualTo(GiftCertificateRules.MaxAmount)
            .Must(a => a is null || decimal.Round(a.Value, 2) == a.Value).WithMessage("The balance can have at most 2 decimals.");
        RuleFor(x => x.AdjustBy).Must(a => a is null || decimal.Round(a.Value, 2) == a.Value).WithMessage("The adjustment can have at most 2 decimals.");
        RuleFor(x => x).Must(x => x.NewBalance is null || x.AdjustBy is null)
            .WithName("NewBalance").WithMessage("Enter either a new balance or an adjustment, not both.");
        RuleFor(x => x.RecipientName).MaximumLength(75);
        RuleFor(x => x.RecipientEmail).MaximumLength(75).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.RecipientEmail));
        RuleFor(x => x.Message).MaximumLength(1000);
        RuleFor(x => x.Comment).MaximumLength(255);
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public class UpdateGiftCertificateHandler : IRequestHandler<UpdateGiftCertificateCommand, GiftCertificateDto>
{
    private const string ConflictMessage = "This gift certificate was changed by someone else. Reload?";

    private readonly IApplicationDbContext _db;
    private readonly IGiftCertificateService _service;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public UpdateGiftCertificateHandler(IApplicationDbContext db, IGiftCertificateService service, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _service = service;
        _user = user;
        _clock = clock;
    }

    public async Task<GiftCertificateDto> Handle(UpdateGiftCertificateCommand request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(_db, request.StoreId, ct);
        var gc = await _db.GiftCertificates.FirstOrDefaultAsync(g => g.Id == request.Id && g.StoreId == storeId, ct)
                 ?? throw new NotFoundException(nameof(GiftCertificate), request.Id);

        byte[] expected;
        try { expected = Convert.FromBase64String(request.RowVersion!); }
        catch (FormatException) { throw new ConflictException(ConflictMessage); }
        if (!gc.RowVersion.AsSpan().SequenceEqual(expected))
            throw new ConflictException(ConflictMessage);

        var newBalance = request.NewBalance ?? (request.AdjustBy is decimal by ? gc.CurrentBalance + by : gc.CurrentBalance);
        var delta = Math.Round(newBalance - gc.CurrentBalance, 2, MidpointRounding.AwayFromZero);
        var statusChanged = request.Status != gc.Status;

        var failures = new List<FluentValidation.Results.ValidationFailure>();
        if (newBalance < 0)
            failures.Add(new("NewBalance", "The balance cannot be negative."));
        if ((delta != 0 || statusChanged) && string.IsNullOrWhiteSpace(request.Comment))
            failures.Add(new("Comment", "A comment is required when the balance or status changes."));
        if (failures.Count > 0)
            throw new Common.Exceptions.ValidationException(failures);

        var before = new { Status = gc.Status.ToString(), gc.CurrentBalance, gc.ExpiresAtUtc, gc.RecipientName, gc.RecipientEmail, gc.Message };

        await using var tx = _db.Database.CurrentTransaction is null ? await _db.Database.BeginTransactionAsync(ct) : null;

        gc.ExpiresAtUtc = request.ExpiresAt;
        gc.RecipientName = Blank(request.RecipientName);
        gc.RecipientEmail = Blank(request.RecipientEmail);
        gc.Message = Blank(request.Message);
        gc.Status = request.Status;
        gc.UpdatedOnUtc = _clock.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(ConflictMessage);
        }

        if (statusChanged)
            await _service.RecordStatusChangeAsync(gc.Id, $"{before.Status} → {gc.Status}: {request.Comment}", ct);
        if (delta != 0)
            await _service.ApplyBalanceChangeAsync(gc.Id, delta, GiftCertificateAction.AdminAdjusted, null, request.Comment, ct: ct);

        _db.AuditTrails.Add(GiftCertificateAudit.Entry("GiftCertificate.Updated", gc.Id, before,
            new { Status = gc.Status.ToString(), CurrentBalance = newBalance, gc.ExpiresAtUtc, gc.RecipientName, gc.RecipientEmail, gc.Message, request.Comment },
            _user, _clock));
        await _db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);

        return await GiftCertificateReader.LoadAsync(_db, storeId, gc.Id, ct);
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}

// ---- Delete / resend -----------------------------------------------------------------------------------

public record DeleteGiftCertificateCommand(Guid? StoreId, Guid Id) : IRequest;

public class DeleteGiftCertificateHandler : IRequestHandler<DeleteGiftCertificateCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IGiftCertificateService _service;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public DeleteGiftCertificateHandler(IApplicationDbContext db, IGiftCertificateService service, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _service = service;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(DeleteGiftCertificateCommand request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(_db, request.StoreId, ct);
        var gc = await _db.GiftCertificates.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.Id && g.StoreId == storeId, ct)
                 ?? throw new NotFoundException(nameof(GiftCertificate), request.Id);

        var openUnpaid = await _db.Orders.AsNoTracking().AnyAsync(o =>
            o.GiftCertificateId == gc.Id
            && o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Delivered
            && o.PaymentStatus != PaymentStatus.Captured && o.PaymentStatus != PaymentStatus.Refunded
            && o.PaymentStatus != PaymentStatus.PartiallyRefunded, ct);
        if (openUnpaid)
            throw new ConflictException("This gift certificate is on an open, unpaid order and cannot be deleted yet.");

        await _service.VoidAsync(gc.Id, "Deleted by admin", softDelete: true, ct);
        _db.AuditTrails.Add(GiftCertificateAudit.Entry("GiftCertificate.Deleted", gc.Id,
            new { Status = gc.Status.ToString(), gc.CurrentBalance }, null, _user, _clock));
        await _db.SaveChangesAsync(ct);
    }
}

public record ResendGiftCertificateEmailCommand(Guid? StoreId, Guid Id) : IRequest;

public class ResendGiftCertificateEmailHandler : IRequestHandler<ResendGiftCertificateEmailCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly IGiftCertificateMailer _mailer;

    public ResendGiftCertificateEmailHandler(IApplicationDbContext db, IGiftCertificateMailer mailer)
    {
        _db = db;
        _mailer = mailer;
    }

    public async Task Handle(ResendGiftCertificateEmailCommand request, CancellationToken ct)
    {
        var storeId = await StoreScope.ResolveAsync(_db, request.StoreId, ct);
        var gc = await _db.GiftCertificates.AsNoTracking().FirstOrDefaultAsync(g => g.Id == request.Id && g.StoreId == storeId, ct)
                 ?? throw new NotFoundException(nameof(GiftCertificate), request.Id);
        if (string.IsNullOrWhiteSpace(gc.RecipientEmail))
            throw new Common.Exceptions.ValidationException(new[] { new FluentValidation.Results.ValidationFailure("RecipientEmail", "Add a recipient email first.") });
        await _mailer.QueueAsync(gc.Id, ct);
    }
}

// ---- Settings ------------------------------------------------------------------------------------------

public record GetGiftCertificateSettingsQuery() : IRequest<GiftCertificateSettingsDto>;

public class GetGiftCertificateSettingsHandler : IRequestHandler<GetGiftCertificateSettingsQuery, GiftCertificateSettingsDto>
{
    private readonly ISettingsService _settings;

    public GetGiftCertificateSettingsHandler(ISettingsService settings) => _settings = settings;

    public async Task<GiftCertificateSettingsDto> Handle(GetGiftCertificateSettingsQuery request, CancellationToken ct) =>
        await GiftCertificateSettings.LoadAsync(_settings, ct);
}

public record SaveGiftCertificateSettingsCommand(
    string? CodePrefix, int CodeLength, bool AllowRedeemOnGcPurchase, int? DefaultExpiryMonths,
    List<PaymentMethodType>? PurchasePaymentMethods) : IRequest<GiftCertificateSettingsDto>;

public class SaveGiftCertificateSettingsValidator : AbstractValidator<SaveGiftCertificateSettingsCommand>
{
    public SaveGiftCertificateSettingsValidator()
    {
        RuleFor(x => x.CodePrefix).MaximumLength(10).Matches("^[A-Za-z0-9-]*$")
            .WithMessage("The prefix may contain letters, digits and dashes only.");
        RuleFor(x => x.CodeLength).InclusiveBetween(GiftCertificateRules.MinCodeLength, 40)
            .WithMessage("Code length must be between 10 and 40.");
        RuleFor(x => x.DefaultExpiryMonths).InclusiveBetween(1, 120).When(x => x.DefaultExpiryMonths.HasValue);
        RuleFor(x => x.PurchasePaymentMethods).NotEmpty().WithMessage("Allow at least one payment method for gift certificate purchases.");
    }
}

public class SaveGiftCertificateSettingsHandler : IRequestHandler<SaveGiftCertificateSettingsCommand, GiftCertificateSettingsDto>
{
    private readonly ISettingsService _settings;

    public SaveGiftCertificateSettingsHandler(ISettingsService settings) => _settings = settings;

    public async Task<GiftCertificateSettingsDto> Handle(SaveGiftCertificateSettingsCommand request, CancellationToken ct)
    {
        await _settings.SetAsync(GiftCertificateRules.CodePrefixKey, GiftCertificateRules.Normalize(request.CodePrefix), ct);
        await _settings.SetAsync(GiftCertificateRules.CodeLengthKey, request.CodeLength.ToString(CultureInfo.InvariantCulture), ct);
        await _settings.SetAsync(GiftCertificateRules.AllowRedeemOnGcPurchaseKey, request.AllowRedeemOnGcPurchase ? "true" : "false", ct);
        await _settings.SetAsync(GiftCertificateRules.DefaultExpiryMonthsKey, request.DefaultExpiryMonths?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, ct);
        await _settings.SetAsync(GiftCertificateRules.PurchasePaymentMethodsKey,
            string.Join(',', (request.PurchasePaymentMethods ?? new()).Distinct().Where(m => m != PaymentMethodType.GiftCertificate)), ct);
        return await GiftCertificateSettings.LoadAsync(_settings, ct);
    }
}

public static class GiftCertificateSettings
{
    public static async Task<GiftCertificateSettingsDto> LoadAsync(ISettingsService settings, CancellationToken ct)
    {
        var length = int.TryParse(await settings.GetValueAsync(GiftCertificateRules.CodeLengthKey, ct), out var l) ? l : 12;
        var months = int.TryParse(await settings.GetValueAsync(GiftCertificateRules.DefaultExpiryMonthsKey, ct), out var m) && m > 0 ? m : (int?)null;
        return new GiftCertificateSettingsDto
        {
            CodePrefix = await settings.GetValueAsync(GiftCertificateRules.CodePrefixKey, ct) ?? string.Empty,
            CodeLength = Math.Max(GiftCertificateRules.MinCodeLength, length),
            AllowRedeemOnGcPurchase = await AllowRedeemOnGcPurchaseAsync(settings, ct),
            DefaultExpiryMonths = months,
            PurchasePaymentMethods = (await PurchasePaymentMethodsAsync(settings, ct)).ToList(),
        };
    }

    public static async Task<bool> AllowRedeemOnGcPurchaseAsync(ISettingsService settings, CancellationToken ct) =>
        bool.TryParse(await settings.GetValueAsync(GiftCertificateRules.AllowRedeemOnGcPurchaseKey, ct), out var b) && b;

    public static async Task<IReadOnlySet<PaymentMethodType>> PurchasePaymentMethodsAsync(ISettingsService settings, CancellationToken ct)
    {
        var raw = await settings.GetValueAsync(GiftCertificateRules.PurchasePaymentMethodsKey, ct);
        if (string.IsNullOrWhiteSpace(raw)) raw = GiftCertificateRules.DefaultPurchasePaymentMethods;
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Enum.TryParse<PaymentMethodType>(s, true, out var m) ? m : (PaymentMethodType?)null)
            .Where(m => m is not null)
            .Select(m => m!.Value)
            .ToHashSet();
    }
}

internal static class GiftCertificateAudit
{
    public static Domain.Entities.AuditTrail Entry(string action, Guid id, object? before, object? after, ICurrentUserService user, IDateTimeProvider clock) => new()
    {
        UserId = user.UserId,
        ActorName = user.Email,
        Action = action,
        EntityType = nameof(GiftCertificate),
        EntityId = id.ToString(),
        MetadataJson = JsonSerializer.Serialize(new { before, after }),
        TimestampUtc = clock.UtcNow,
    };
}
