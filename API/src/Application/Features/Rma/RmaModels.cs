using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>A return in a list (admin queue / My Returns).</summary>
public class RmaSummaryDto
{
    public Guid Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public string? OrderNumber { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public RmaStatus Status { get; set; }
    public RmaResolution RequestedResolution { get; set; }
    public RmaResolution Resolution { get; set; }
    public RmaSource Source { get; set; }
    public bool RequiresFfl { get; set; }
    public int LineCount { get; set; }
    public int UnitCount { get; set; }
    public decimal TotalRefund { get; set; }
    public decimal? RefundedAmount { get; set; }
    public string? LastRefundError { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateTime RequestedOnUtc { get; set; }
    public DateTime? StatusChangedOnUtc { get; set; }
    /// <summary>RT-31: days in the current status.</summary>
    public int DaysInStatus { get; set; }

    public static RmaSummaryDto From(Domain.Entities.Rma r, Order? order, DateTime now)
    {
        var since = r.StatusChangedOnUtc ?? r.RequestedOnUtc;
        return new RmaSummaryDto
        {
            Id = r.Id,
            RmaNumber = r.RmaNumber,
            OrderId = r.OrderId,
            OrderNumber = order?.OrderNumber,
            CustomerId = r.CustomerId,
            CustomerName = order?.ContactName,
            CustomerEmail = order?.ContactEmail,
            Status = r.Status,
            RequestedResolution = r.RequestedResolution,
            Resolution = r.Resolution,
            Source = r.Source,
            RequiresFfl = r.RequiresFfl,
            LineCount = r.Lines.Count,
            UnitCount = r.Lines.Sum(l => l.Quantity),
            TotalRefund = r.TotalRefund,
            RefundedAmount = r.RefundedAmount,
            LastRefundError = r.LastRefundError,
            CurrencyCode = order?.CurrencyCode ?? "USD",
            RequestedOnUtc = r.RequestedOnUtc,
            StatusChangedOnUtc = r.StatusChangedOnUtc,
            DaysInStatus = Math.Max(0, (int)(now - since).TotalDays),
        };
    }
}

/// <summary>A return with everything the detail page needs. Customer views strip staff-only data (<see cref="ForCustomer"/>).</summary>
public class RmaDto : RmaSummaryDto
{
    public RmaReturnMethod ReturnMethod { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ResolutionNotes { get; set; }

    public bool ShipRestricted { get; set; }
    public string? FflDealerName { get; set; }
    public string? FflNumber { get; set; }
    public DateTime? FflExpiry { get; set; }
    public string? EligibilityOverrideReason { get; set; }

    public string? LabelCarrier { get; set; }
    public string? LabelService { get; set; }
    public string? TrackingNumber { get; set; }
    public string? LabelUrl { get; set; }
    public decimal? LabelCost { get; set; }
    public DateTime? LabelExpiresAtUtc { get; set; }
    public string? ReturnInstructions { get; set; }

    /// <summary>The frozen breakdown after inspection, or a live estimate before it.</summary>
    public RmaRefundBreakdown Refund { get; set; } = new();
    public bool RefundShipping { get; set; }
    public bool WaiveRestockFee { get; set; }
    public string? AdjustmentReason { get; set; }
    public RmaRefundMethod RefundMethod { get; set; }
    public string? RefundReference { get; set; }
    public decimal? StoreCreditIssued { get; set; }
    public Guid? ReplacementOrderId { get; set; }
    public string? ReplacementOrderNumber { get; set; }
    public int RefundAttempts { get; set; }
    /// <summary>What can still be refunded on the order's payments (gateway + gift certificate).</summary>
    public decimal OrderRefundableBalance { get; set; }

    public DateTime? ApprovedOnUtc { get; set; }
    public DateTime? ReceivedOnUtc { get; set; }
    public DateTime? RefundedOnUtc { get; set; }
    public DateTime? ClosedOnUtc { get; set; }

    public string? ShipToSummary { get; set; }
    public List<RmaLineItemDto> Lines { get; set; } = new();
    public List<RmaHistoryDto> History { get; set; } = new();
    public List<RmaMessageDto> Messages { get; set; } = new();
    public List<RmaAttachmentDto> Attachments { get; set; } = new();
    /// <summary>Actions valid in the current status (drives the buttons; the server re-checks every one).</summary>
    public List<string> AvailableActions { get; set; } = new();

    public static RmaDto From(Domain.Entities.Rma r, Order? order, RmaRefundBreakdown refund, DateTime now)
    {
        var s = RmaSummaryDto.From(r, order, now);
        var dto = new RmaDto();
        foreach (var p in typeof(RmaSummaryDto).GetProperties())
            p.SetValue(dto, p.GetValue(s));

        dto.ReturnMethod = r.ReturnMethod;
        dto.Reason = r.Reason;
        dto.ResolutionNotes = r.ResolutionNotes;
        dto.ShipRestricted = r.ShipRestricted;
        dto.FflDealerName = r.FflDealerName;
        dto.FflNumber = r.FflNumber;
        dto.FflExpiry = r.FflExpiry;
        dto.EligibilityOverrideReason = r.EligibilityOverrideReason;
        dto.LabelCarrier = r.LabelCarrier;
        dto.LabelService = r.LabelService;
        dto.TrackingNumber = r.TrackingNumber;
        dto.LabelUrl = r.LabelUrl;
        dto.LabelCost = r.LabelCost;
        dto.LabelExpiresAtUtc = r.LabelExpiresAtUtc;
        dto.ReturnInstructions = r.ReturnInstructions;
        dto.Refund = refund;
        dto.RefundShipping = r.RefundShipping;
        dto.WaiveRestockFee = r.WaiveRestockFee;
        dto.AdjustmentReason = r.AdjustmentReason;
        dto.RefundMethod = r.RefundMethod;
        dto.RefundReference = r.RefundReference;
        dto.StoreCreditIssued = r.StoreCreditIssued;
        dto.ReplacementOrderId = r.ReplacementOrderId;
        dto.RefundAttempts = r.RefundAttempts;
        dto.ApprovedOnUtc = r.ApprovedOnUtc;
        dto.ReceivedOnUtc = r.ReceivedOnUtc;
        dto.RefundedOnUtc = r.RefundedOnUtc;
        dto.ClosedOnUtc = r.ClosedOnUtc;
        if (order?.ShippingAddress is { } a)
            dto.ShipToSummary = string.Join(", ", new[] { a.AddressLine1, a.City, a.StateProvince, a.PostalCode }.Where(x => !string.IsNullOrWhiteSpace(x)));
        dto.Lines = r.Lines.OrderBy(l => l.ProductName).Select(RmaLineItemDto.From).ToList();
        dto.History = r.History.OrderBy(h => h.CreatedOnUtc).Select(RmaHistoryDto.From).ToList();
        dto.Messages = r.Messages.OrderBy(m => m.CreatedOnUtc).Select(RmaMessageDto.From).ToList();
        dto.Attachments = r.Attachments.OrderBy(a2 => a2.CreatedOnUtc).Select(RmaAttachmentDto.From).ToList();
        dto.AvailableActions = RmaActions.For(r);
        return dto;
    }

    /// <summary>The customer's view: no internal notes, staff names, IPs, FFL or refund diagnostics (RT-35).</summary>
    public RmaDto ForCustomer()
    {
        Messages = Messages.Where(m => !m.IsInternal).ToList();
        foreach (var m in Messages.Where(m => m.AuthorType == RmaActorType.Admin))
            m.AuthorName = "Customer service";
        History = History
            .Where((h, i) => i == 0 || h.FromStatus != h.ToStatus) // the request itself + status changes only
            .Select(h => new RmaHistoryDto { Id = h.Id, FromStatus = h.FromStatus, ToStatus = h.ToStatus, CreatedOnUtc = h.CreatedOnUtc, ActorType = h.ActorType })
            .ToList();
        AvailableActions = RmaActions.ForCustomer(this);
        LastRefundError = null;
        EligibilityOverrideReason = null;
        FflNumber = null;
        FflDealerName = null;
        FflExpiry = null;
        CustomerEmail = null;
        LabelCost = null;
        RefundAttempts = 0;
        OrderRefundableBalance = 0;
        return this;
    }
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
    public int QuantityReceived { get; set; }
    public int QuantityAccepted { get; set; }
    public int QuantityRestocked { get; set; }
    public Guid? ReasonId { get; set; }
    public string? LineReason { get; set; }
    public string? CustomerComment { get; set; }
    public RmaItemCondition? ConditionReported { get; set; }
    public RmaReceivedCondition? ConditionReceived { get; set; }
    public RmaDisposition? Disposition { get; set; }
    public decimal UnitRefund { get; set; }
    public decimal Deduction { get; set; }
    public string? DeductionReason { get; set; }
    public decimal LineRefund { get; set; }
    public bool FeeApplies { get; set; }
    public bool MerchantFault { get; set; }

    public static RmaLineItemDto From(RmaLineItem l) => new()
    {
        Id = l.Id,
        OrderLineItemId = l.OrderLineItemId,
        ProductId = l.ProductId,
        ProductVariantId = l.ProductVariantId,
        ProductName = l.ProductName,
        Sku = l.Sku,
        Quantity = l.Quantity,
        QuantityReceived = l.QuantityReceived,
        QuantityAccepted = l.QuantityAccepted,
        QuantityRestocked = l.QuantityRestocked,
        ReasonId = l.ReasonId,
        LineReason = l.LineReason,
        CustomerComment = l.CustomerComment,
        ConditionReported = l.ConditionReported,
        ConditionReceived = l.ConditionReceived,
        Disposition = l.Disposition,
        UnitRefund = l.UnitRefund,
        Deduction = l.Deduction,
        DeductionReason = l.DeductionReason,
        LineRefund = l.LineRefund,
        FeeApplies = l.FeeApplies,
        MerchantFault = l.MerchantFault,
    };
}

public class RmaHistoryDto
{
    public Guid Id { get; set; }
    public RmaStatus? FromStatus { get; set; }
    public RmaStatus ToStatus { get; set; }
    public RmaActorType ActorType { get; set; }
    public string? ActorName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static RmaHistoryDto From(RmaHistory h) => new()
    {
        Id = h.Id,
        FromStatus = h.FromStatus,
        ToStatus = h.ToStatus,
        ActorType = h.ActorType,
        ActorName = h.ActorName,
        Note = h.Note,
        CreatedOnUtc = h.CreatedOnUtc,
    };
}

public class RmaMessageDto
{
    public Guid Id { get; set; }
    public RmaActorType AuthorType { get; set; }
    public string? AuthorName { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternal { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static RmaMessageDto From(RmaMessage m) => new()
    {
        Id = m.Id,
        AuthorType = m.AuthorType,
        AuthorName = m.AuthorName,
        Body = m.Body,
        IsInternal = m.IsInternal,
        CreatedOnUtc = m.CreatedOnUtc,
    };
}

public class RmaAttachmentDto
{
    public Guid Id { get; set; }
    public Guid? RmaLineItemId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public RmaActorType UploadedByType { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static RmaAttachmentDto From(RmaAttachment a) => new()
    {
        Id = a.Id,
        RmaLineItemId = a.RmaLineItemId,
        OriginalFileName = a.OriginalFileName,
        MimeType = a.MimeType,
        SizeBytes = a.SizeBytes,
        UploadedByType = a.UploadedByType,
        CreatedOnUtc = a.CreatedOnUtc,
    };
}

/// <summary>Which actions the UI should offer for a return (RT-33). Handlers enforce the same rules.</summary>
public static class RmaActions
{
    public const string Approve = "approve";
    public const string Reject = "reject";
    public const string RequestInfo = "requestInfo";
    public const string EditLines = "editLines";
    public const string SetFfl = "setFfl";
    public const string SetLabel = "setLabel";
    public const string MarkInTransit = "markInTransit";
    public const string Receive = "receive";
    public const string Inspect = "inspect";
    public const string Refund = "refund";
    public const string StoreCredit = "storeCredit";
    public const string Exchange = "exchange";
    public const string Close = "close";
    public const string Reopen = "reopen";
    public const string Cancel = "cancel";
    public const string Message = "message";
    public const string SetTracking = "setTracking";

    public static List<string> For(Domain.Entities.Rma r)
    {
        var a = new List<string> { Message };
        switch (r.Status)
        {
            case RmaStatus.Requested:
                a.AddRange(new[] { Approve, Reject, RequestInfo, EditLines, Receive, Cancel });
                if (r.RequiresFfl) a.Add(SetFfl);
                break;
            case RmaStatus.Approved:
                a.AddRange(new[] { SetLabel, MarkInTransit, Receive, EditLines, Reject, Cancel });
                if (r.RequiresFfl) a.Add(SetFfl);
                break;
            case RmaStatus.InTransit:
                a.AddRange(new[] { SetLabel, Receive });
                break;
            case RmaStatus.Received:
                a.AddRange(new[] { Receive, Inspect, Close });
                break;
            case RmaStatus.Inspected:
                a.AddRange(new[] { Inspect, Refund, Exchange, Close });
                if (r.CustomerId is not null) a.Add(StoreCredit);
                break;
            case RmaStatus.Refunded:
            case RmaStatus.Exchanged:
                a.Add(Close);
                break;
            case RmaStatus.Closed:
                a.Add(Reopen);
                break;
        }
        return a;
    }

    public static List<string> ForCustomer(RmaDto r)
    {
        var a = new List<string>();
        if (r.Status is RmaStatus.Requested or RmaStatus.Approved)
            a.Add(Cancel);
        if (RmaWorkflow.IsOpen(r.Status) && r.Status is not (RmaStatus.Refunded or RmaStatus.Exchanged))
            a.Add(Message);
        if (r.Status == RmaStatus.Approved && r.ReturnMethod == RmaReturnMethod.SelfShip)
            a.Add(SetTracking);
        return a;
    }
}
