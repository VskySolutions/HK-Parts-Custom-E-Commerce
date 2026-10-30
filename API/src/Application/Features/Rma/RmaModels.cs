using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>A return merchandise authorization and its line items (REQ-ORD-004).</summary>
public class RmaDto
{
    public Guid Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public RmaStatus Status { get; set; }
    public RmaResolution Resolution { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }
    public DateTime RequestedOnUtc { get; set; }
    public DateTime? ResolvedOnUtc { get; set; }
    public decimal? RefundedAmount { get; set; }
    public decimal? StoreCreditIssued { get; set; }
    public Guid? ReplacementOrderId { get; set; }
    public List<RmaLineItemDto> Lines { get; set; } = new();

    public static RmaDto From(HKPartsEcommerce.Domain.Entities.Rma r) => new()
    {
        Id = r.Id,
        RmaNumber = r.RmaNumber,
        OrderId = r.OrderId,
        CustomerId = r.CustomerId,
        Status = r.Status,
        Resolution = r.Resolution,
        Reason = r.Reason,
        ResolutionNotes = r.ResolutionNotes,
        RequestedOnUtc = r.RequestedOnUtc,
        ResolvedOnUtc = r.ResolvedOnUtc,
        RefundedAmount = r.RefundedAmount,
        StoreCreditIssued = r.StoreCreditIssued,
        ReplacementOrderId = r.ReplacementOrderId,
        Lines = r.Lines.Select(RmaLineItemDto.From).ToList(),
    };
}

public class RmaLineItemDto
{
    public Guid Id { get; set; }
    public Guid OrderLineItemId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public int Quantity { get; set; }
    public string? LineReason { get; set; }

    public static RmaLineItemDto From(RmaLineItem l) => new()
    {
        Id = l.Id,
        OrderLineItemId = l.OrderLineItemId,
        ProductId = l.ProductId,
        ProductVariantId = l.ProductVariantId,
        ProductName = l.ProductName,
        Sku = l.Sku,
        Quantity = l.Quantity,
        LineReason = l.LineReason,
    };
}
