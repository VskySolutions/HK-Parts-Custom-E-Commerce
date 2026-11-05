using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.GiftCertificates;

public class GiftCertificateListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public DateTime CreatedOnUtc { get; set; }
    public GiftCertificateSource Source { get; set; }
    public Guid? SourceOrderId { get; set; }
    public string? SourceOrderNumber { get; set; }
    /// <summary>Billing (contact) name of the <em>purchasing</em> order — never an order that redeemed it.</summary>
    public string? PurchasedBy { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public decimal Amount { get; set; }
    public decimal Spent { get; set; }
    public decimal Balance { get; set; }
    public GiftCertificateStatus Status { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}

public class GiftCertificateLedgerDto
{
    public Guid Id { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public GiftCertificateAction Action { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public string? Comment { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class GiftCertificateOrderDto
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime PlacedOnUtc { get; set; }
    public string? BillTo { get; set; }
    public string? ShipTo { get; set; }
    public decimal OrderTotal { get; set; }
    public decimal AppliedAmount { get; set; }
    public bool Paid { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Set for an order imported from eBay (channel icon).</summary>
    public string? EbayOrderId { get; set; }
}

public class GiftCertificateDto
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public string Code { get; set; } = string.Empty;
    public GiftCertificateSource Source { get; set; }
    public Guid? SourceOrderId { get; set; }
    public string? SourceOrderNumber { get; set; }
    public string? PurchasedBy { get; set; }
    public decimal OriginalBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public GiftCertificateStatus Status { get; set; }
    public bool IsOnline { get; set; }
    public string? SenderName { get; set; }
    public string? SenderEmail { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public List<GiftCertificateLedgerDto> Ledger { get; set; } = new();
    public List<GiftCertificateOrderDto> Orders { get; set; } = new();

    public object ToAuditSnapshot() => new
    {
        Status = Status.ToString(), CurrentBalance, ExpiresAtUtc, RecipientName, RecipientEmail, Message,
    };
}

/// <summary>The redeemable certificate on a cart (code, balance and what it covers now).</summary>
public class CartGiftCertificateDto
{
    public string Code { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public decimal ApplyAmount { get; set; }
    public decimal RemainingTotal { get; set; }
}

public class GiftCertificateSettingsDto
{
    public string CodePrefix { get; set; } = string.Empty;
    public int CodeLength { get; set; } = 12;
    public bool AllowRedeemOnGcPurchase { get; set; }
    public int? DefaultExpiryMonths { get; set; }
    /// <summary>Payment methods offered when the cart holds a certificate product (AllowGiftCertificatePurchase).</summary>
    public List<PaymentMethodType> PurchasePaymentMethods { get; set; } = new();
}

/// <summary>A certificate issued by an order line (admin order view: staff mail physical cards).</summary>
public class IssuedGiftCertificateDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
    public bool IsOnline { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public DateTime? EmailSentAtUtc { get; set; }
    public GiftCertificateStatus Status { get; set; }

    public static IssuedGiftCertificateDto From(GiftCertificate g) => new()
    {
        Id = g.Id,
        Code = g.Code,
        Amount = g.OriginalBalance,
        Balance = g.CurrentBalance,
        IsOnline = g.IsOnline,
        RecipientName = g.RecipientName,
        RecipientEmail = g.RecipientEmail,
        EmailSentAtUtc = g.EmailSentAtUtc,
        Status = g.Status,
    };
}
