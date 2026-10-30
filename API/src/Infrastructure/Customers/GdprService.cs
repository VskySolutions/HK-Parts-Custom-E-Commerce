using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Customers;

/// <summary>
/// GDPR data-subject operations (WO-23). Export builds a portable JSON snapshot of everything the platform
/// holds about a customer; anonymisation scrubs personal data in place while leaving order records — and the
/// geography those records need for accounting — intact. Nothing is physically deleted here beyond soft-delete
/// flags: order rows are never removed and financial totals are untouched.
/// </summary>
public class GdprService : IGdprService
{
    // Fixed anonymisation tokens so scrubbed rows read intelligibly rather than as empty strings.
    private const string AnonymisedFirstName = "Deleted";
    private const string AnonymisedLastName = "Customer";
    private const string AnonymisedFullName = "Deleted Customer";
    private const string RedactedLine = "Redacted";

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly IPasswordHasher _hasher;

    public GdprService(IApplicationDbContext db, IDateTimeProvider clock, IPasswordHasher hasher)
    {
        _db = db;
        _clock = clock;
        _hasher = hasher;
    }

    public async Task<byte[]> ExportCustomerDataAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        var email = customer.User?.Email;

        var addresses = await _db.CustomerAddresses
            .AsNoTracking()
            .Include(ca => ca.Address)
            .Where(ca => ca.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        var orders = await _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.Lines)
            .Include(o => o.ShippingAddress)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedOnUtc)
            .ToListAsync(cancellationToken);

        var reviews = await _db.ProductReviews
            .AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        var questions = await _db.ProductQuestions
            .AsNoTracking()
            .Where(q => q.CustomerId == customerId)
            .OrderByDescending(q => q.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        // Newsletter subscriptions are keyed by email, not customer id (CMS-owned table).
        var newsletter = string.IsNullOrWhiteSpace(email)
            ? new List<CMSNewsletterSubscription>()
            : await _db.CMSNewsletterSubscriptions
                .AsNoTracking()
                .Where(n => n.Email == email)
                .ToListAsync(cancellationToken);

        var document = new GdprExportDocument
        {
            GeneratedOnUtc = _clock.UtcNow,
            Profile = new GdprProfileSection
            {
                CustomerId = customer.Id,
                Email = email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                PhoneNumber = customer.PhoneNumber,
                PreferredTimeZone = customer.PreferredTimeZone,
                IsTaxExempt = customer.IsTaxExempt,
                VatId = customer.VatId,
                WhatsAppPhoneNumber = customer.WhatsAppPhoneNumber,
                WhatsAppOptIn = customer.WhatsAppOptIn,
                EmailVerified = customer.User?.EmailVerified,
                AccountCreatedOnUtc = customer.CreatedOnUtc,
            },
            Addresses = addresses
                .Where(ca => ca.Address is not null)
                .Select(ca => new GdprAddressSection
                {
                    Type = ca.AddressType.ToString(),
                    IsDefault = ca.IsDefault,
                    FirstName = ca.Address!.FirstName,
                    LastName = ca.Address.LastName,
                    Company = ca.Address.Company,
                    AddressLine1 = ca.Address.AddressLine1,
                    AddressLine2 = ca.Address.AddressLine2,
                    Landmark = ca.Address.Landmark,
                    City = ca.Address.City,
                    StateProvince = ca.Address.StateProvince,
                    PostalCode = ca.Address.PostalCode,
                    CountryCode = ca.Address.CountryCode,
                    PhoneNumber = ca.Address.PhoneNumber,
                    Email = ca.Address.Email,
                })
                .ToList(),
            Orders = orders.Select(o => new GdprOrderSection
            {
                OrderNumber = o.OrderNumber,
                Status = o.Status.ToString(),
                PaymentStatus = o.PaymentStatus.ToString(),
                PlacedOnUtc = o.PlacedOnUtc,
                CurrencyCode = o.CurrencyCode,
                Subtotal = o.Subtotal,
                DiscountTotal = o.DiscountTotal,
                ShippingTotal = o.ShippingTotal,
                TaxTotal = o.TaxTotal,
                TotalAmount = o.TotalAmount,
                ShipToName = o.ContactName,
                ShipToCity = o.City,
                ShipToStateProvince = o.StateProvince,
                ShipToCountryCode = o.CountryCode,
                Lines = o.Lines.Select(l => new GdprOrderLineSection
                {
                    ProductName = l.ProductName,
                    Sku = l.Sku,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal,
                    CustomAttributes = CustomAttributes.Parse(l.CustomAttributesJson),
                }).ToList(),
            }).ToList(),
            Reviews = reviews.Select(r => new GdprReviewSection
            {
                ProductId = r.ProductId,
                Rating = r.Rating,
                Title = r.Title,
                Body = r.Body,
                Status = r.Status.ToString(),
                ReviewerName = r.ReviewerName,
                SubmittedOnUtc = r.CreatedOnUtc,
            }).ToList(),
            Questions = questions.Select(q => new GdprQuestionSection
            {
                ProductId = q.ProductId,
                QuestionText = q.QuestionText,
                Status = q.Status.ToString(),
                AnswerText = q.AnswerText,
                AskerName = q.AskerName,
                AskerEmail = q.AskerEmail,
                SubmittedOnUtc = q.CreatedOnUtc,
            }).ToList(),
            NewsletterSubscriptions = newsletter.Select(n => new GdprNewsletterSection
            {
                Email = n.Email,
                Status = n.Status.ToString(),
                Source = n.Source,
                ConfirmedOnUtc = n.ConfirmedOnUtc,
                UnsubscribedOnUtc = n.UnsubscribedOnUtc,
            }).ToList(),
        };

        var json = JsonSerializer.Serialize(document, ExportJsonOptions);
        return Encoding.UTF8.GetBytes(json);
    }

    public async Task AnonymizeCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), customerId);

        var now = _clock.UtcNow;

        // 1) Profile: blank the personal identifiers, drop marketing/tax PII, and soft-delete the profile.
        customer.FirstName = AnonymisedFirstName;
        customer.LastName = AnonymisedLastName;
        customer.PhoneNumber = null;
        customer.PreferredTimeZone = null;
        customer.WhatsAppPhoneNumber = null;
        customer.WhatsAppOptIn = false;
        customer.WhatsAppOptInAtUtc = null;
        customer.VatId = null;
        customer.TaxExemptionCertificate = null;
        customer.Deleted = true;
        customer.DeletedOnUtc = now;

        // 2) Login identity: replace the email with a non-reversible placeholder, scramble the credential,
        //    and disable the account so it can never authenticate again.
        if (customer.User is { } user)
        {
            var opaque = Guid.NewGuid().ToString("N");
            user.Email = $"deleted-{opaque}@anonymized.invalid";
            user.Username = $"deleted-{opaque}";
            user.PasswordHash = _hasher.Hash(Guid.NewGuid().ToString());
            user.EmailVerified = false;
            user.IsActive = false;            // login query requires IsActive == true
            user.Deleted = true;              // retire the identity via the soft-delete filter
            user.DeletedOnUtc = now;
        }

        // 3) Address book: redact the personal fields but keep the geography (city/region/postal/country)
        //    so any order that references the row stays valid for accounting.
        var customerAddresses = await _db.CustomerAddresses
            .Include(ca => ca.Address)
            .Where(ca => ca.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        foreach (var address in customerAddresses.Select(ca => ca.Address).Where(a => a is not null))
        {
            address!.FirstName = AnonymisedFirstName;
            address.LastName = AnonymisedLastName;
            address.Company = null;
            address.AddressLine1 = RedactedLine;
            address.AddressLine2 = null;
            address.Landmark = null;
            address.PhoneNumber = null;
            address.Email = null;
            address.Latitude = null;
            address.Longitude = null;
        }

        // 4) Public engagement snapshots carry the customer's name/email verbatim — scrub those too. The
        //    review/question bodies stay (they are content, not identifiers) but are no longer attributable.
        var reviews = await _db.ProductReviews
            .Where(r => r.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        foreach (var review in reviews)
            review.ReviewerName = AnonymisedFullName;

        var questions = await _db.ProductQuestions
            .Where(q => q.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        foreach (var question in questions)
        {
            question.AskerName = AnonymisedFullName;
            question.AskerEmail = null;
        }

        // Single atomic commit for the whole erasure.
        await _db.SaveChangesAsync(cancellationToken);
    }
}

// ---- Export document shape (serialisation-only DTOs) --------------------------------------------------
// Internal records with public properties; System.Text.Json serialises the public surface regardless of
// the declaring type's accessibility. Kept in this file because the JSON contract is local to the export.

internal sealed record GdprExportDocument
{
    public DateTime GeneratedOnUtc { get; init; }
    public GdprProfileSection Profile { get; init; } = new();
    public List<GdprAddressSection> Addresses { get; init; } = new();
    public List<GdprOrderSection> Orders { get; init; } = new();
    public List<GdprReviewSection> Reviews { get; init; } = new();
    public List<GdprQuestionSection> Questions { get; init; } = new();
    public List<GdprNewsletterSection> NewsletterSubscriptions { get; init; } = new();
}

internal sealed record GdprProfileSection
{
    public Guid CustomerId { get; init; }
    public string? Email { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? PreferredTimeZone { get; init; }
    public bool IsTaxExempt { get; init; }
    public string? VatId { get; init; }
    public string? WhatsAppPhoneNumber { get; init; }
    public bool WhatsAppOptIn { get; init; }
    public bool? EmailVerified { get; init; }
    public DateTime AccountCreatedOnUtc { get; init; }
}

internal sealed record GdprAddressSection
{
    public string Type { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Company { get; init; }
    public string AddressLine1 { get; init; } = string.Empty;
    public string? AddressLine2 { get; init; }
    public string? Landmark { get; init; }
    public string City { get; init; } = string.Empty;
    public string? StateProvince { get; init; }
    public string PostalCode { get; init; } = string.Empty;
    public string CountryCode { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
}

internal sealed record GdprOrderSection
{
    public string OrderNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public DateTime PlacedOnUtc { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal ShippingTotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal TotalAmount { get; init; }
    public string? ShipToName { get; init; }
    public string? ShipToCity { get; init; }
    public string? ShipToStateProvince { get; init; }
    public string? ShipToCountryCode { get; init; }
    public List<GdprOrderLineSection> Lines { get; init; } = new();
}

internal sealed record GdprOrderLineSection
{
    public string ProductName { get; init; } = string.Empty;
    public string? Sku { get; init; }
    public int Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineTotal { get; init; }

    /// <summary>Anything the customer typed for this line (engraving, gift note, …) — their own data, so it belongs in the export.</summary>
    public List<CustomAttributeSelection> CustomAttributes { get; init; } = new();
}

internal sealed record GdprReviewSection
{
    public Guid ProductId { get; init; }
    public int Rating { get; init; }
    public string? Title { get; init; }
    public string Body { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string ReviewerName { get; init; } = string.Empty;
    public DateTime SubmittedOnUtc { get; init; }
}

internal sealed record GdprQuestionSection
{
    public Guid ProductId { get; init; }
    public string QuestionText { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? AnswerText { get; init; }
    public string AskerName { get; init; } = string.Empty;
    public string? AskerEmail { get; init; }
    public DateTime SubmittedOnUtc { get; init; }
}

internal sealed record GdprNewsletterSection
{
    public string Email { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Source { get; init; }
    public DateTime? ConfirmedOnUtc { get; init; }
    public DateTime? UnsubscribedOnUtc { get; init; }
}
