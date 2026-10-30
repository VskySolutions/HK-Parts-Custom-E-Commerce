using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Inquiries;

/// <summary>A row in the admin Inquiries list (REQ-INQ-001) — lead-shaped, not order-shaped.</summary>
public class InquirySummaryDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string InquiryStatus { get; set; } = string.Empty;
    public DateTime SubmittedOnUtc { get; set; }

    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? CompanyName { get; set; }

    public Guid? AssignedStoreId { get; set; }
    public string? AssignedStoreName { get; set; }

    public int ItemCount { get; set; }
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>The priced value of the request (subtotal less discounts). Not revenue — see ExcludeInquiries.</summary>
    public decimal EstimatedValue { get; set; }

    public bool HasBeenQuoted { get; set; }

    public static InquirySummaryDto From(Order o) => new()
    {
        Id = o.Id,
        ReferenceNumber = o.OrderNumber,
        InquiryStatus = o.InquiryStatus?.ToString() ?? Domain.Enums.InquiryStatus.New.ToString(),
        SubmittedOnUtc = o.PlacedOnUtc,
        ContactName = o.ContactName,
        ContactEmail = o.ContactEmail,
        ContactPhone = o.ContactPhone,
        CompanyName = o.CompanyName,
        AssignedStoreId = o.AssignedStoreId,
        AssignedStoreName = o.AssignedStore?.Name,
        ItemCount = o.Lines.Count,
        CurrencyCode = o.CurrencyCode,
        EstimatedValue = o.TotalAmount,
        HasBeenQuoted = o.QuotedOnUtc != null,
    };
}

/// <summary>The full inquiry as the admin detail page shows it: who asked, for what, and where it stands.</summary>
public class InquiryDto
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string InquiryStatus { get; set; } = string.Empty;
    public DateTime SubmittedOnUtc { get; set; }
    public DateTime? QuotedOnUtc { get; set; }

    // ---- Customer information (the point of the inquiry) ----
    public Guid? CustomerId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? CompanyName { get; set; }
    public string? PreferredContact { get; set; }
    public DateTime? RequiredByUtc { get; set; }
    public string? Message { get; set; }
    public string? InternalNotes { get; set; }

    // ---- Address (blank in contact-only mode) ----
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? City { get; set; }
    public string? StateProvince { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }

    public Guid? AssignedStoreId { get; set; }
    public string? AssignedStoreName { get; set; }

    // ---- Requested items ----
    public string CurrencyCode { get; set; } = "USD";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal EstimatedValue { get; set; }
    public string? AppliedCouponCode { get; set; }
    public List<OrderLineItemDto> Lines { get; set; } = new();

    /// <summary>The order this inquiry became, once converted; null otherwise.</summary>
    public Guid? ConvertedOrderId { get; set; }

    public static InquiryDto From(Order o) => new()
    {
        Id = o.Id,
        ReferenceNumber = o.OrderNumber,
        InquiryStatus = o.InquiryStatus?.ToString() ?? Domain.Enums.InquiryStatus.New.ToString(),
        SubmittedOnUtc = o.PlacedOnUtc,
        QuotedOnUtc = o.QuotedOnUtc,
        CustomerId = o.CustomerId,
        ContactName = o.ContactName,
        ContactEmail = o.ContactEmail,
        ContactPhone = o.ContactPhone,
        CompanyName = o.CompanyName,
        PreferredContact = o.PreferredContact?.ToString(),
        RequiredByUtc = o.RequiredByUtc,
        Message = o.CustomerNote,
        InternalNotes = o.InternalNotes,
        AddressLine1 = o.AddressLine1,
        AddressLine2 = o.AddressLine2,
        Landmark = o.Landmark,
        City = o.City,
        StateProvince = o.StateProvince,
        PostalCode = o.PostalCode,
        CountryCode = o.CountryCode,
        AssignedStoreId = o.AssignedStoreId,
        AssignedStoreName = o.AssignedStore?.Name,
        CurrencyCode = o.CurrencyCode,
        Subtotal = o.Subtotal,
        DiscountTotal = o.DiscountTotal,
        EstimatedValue = o.TotalAmount,
        AppliedCouponCode = o.AppliedCouponCode,
        Lines = o.Lines.Select(l => new OrderLineItemDto
        {
            Id = l.Id,
            ProductId = l.ProductId,
            ProductVariantId = l.ProductVariantId,
            ProductName = l.ProductName,
            Sku = l.Sku,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            OriginalUnitPrice = l.OriginalUnitPrice,
            DiscountAmount = l.DiscountAmount,
            LineTotal = l.LineTotal,
            CustomAttributes = CustomAttributes.Parse(l.CustomAttributesJson),
        }).ToList(),
        // An inquiry converts in place, so once converted this is the same row.
        ConvertedOrderId = o.IsInquiry ? null : o.Id,
    };
}
