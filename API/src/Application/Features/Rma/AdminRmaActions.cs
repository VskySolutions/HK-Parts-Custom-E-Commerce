using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;
using ValidationException = HKPartsEcommerce.Application.Common.Exceptions.ValidationException;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>Common dependencies of the admin RMA action handlers.</summary>
public abstract class RmaAdminHandlerBase
{
    protected readonly IApplicationDbContext Db;
    protected readonly ICurrentUserService Current;
    protected readonly ISettingsService SettingsService;
    protected readonly IEmailTemplateSender Templates;
    protected readonly IDateTimeProvider Clock;

    protected RmaAdminHandlerBase(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
    {
        Db = db;
        Current = current;
        SettingsService = settings;
        Templates = templates;
        Clock = clock;
    }

    protected RmaActor Actor => RmaActor.Admin(Current);

    protected Task<RmaDto> DtoAsync(Domain.Entities.Rma rma, Order order, CancellationToken ct)
        => RmaSupport.ToDtoAsync(Db, SettingsService, rma, order, Clock.UtcNow, ct);

    protected static void Require(Domain.Entities.Rma rma, string action)
    {
        if (!RmaActions.For(rma).Contains(action))
            throw new ConflictException($"That action is not available while the return is {RmaWorkflow.Label(rma.Status)}.");
    }

    /// <summary>Saves, turning a lost optimistic-concurrency race into a clear 409 (RT-67).</summary>
    protected async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await Db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Someone else updated this return at the same time. Reload it and try again.");
        }
    }
}

// =====================================================================================================
// Review
// =====================================================================================================

/// <summary>Approves a requested return (RT-33). Firearm returns need dealer details first (RT-14).</summary>
public record ApproveRmaCommand(Guid RmaId, string? Instructions = null, string? Note = null) : IRequest<RmaDto>;

public class ApproveRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<ApproveRmaCommand, RmaDto>
{
    public ApproveRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(ApproveRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Approve);
        if (rma.RequiresFfl && (string.IsNullOrWhiteSpace(rma.FflNumber) || rma.FflExpiry is null))
            throw new ConflictException("Enter the returning dealer's FFL number and expiry before approving a firearm return.");
        if (rma.RequiresFfl && rma.FflExpiry < Clock.UtcNow.Date)
            throw new ConflictException("The returning dealer's FFL has expired.");

        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var settings = await ReturnSettingsStore.LoadAsync(SettingsService, ct);
        if (!string.IsNullOrWhiteSpace(request.Instructions))
            rma.ReturnInstructions = request.Instructions.Trim();
        RmaApproval.Approve(Db, rma, settings, Actor, request.Note, Clock.UtcNow);
        await SaveAsync(ct);

        await RmaApproval.NotifyApprovedAsync(Templates, settings, order, rma, ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Rejects a return with a customer-visible reason (emailed).</summary>
public record RejectRmaCommand(Guid RmaId, string Reason) : IRequest<RmaDto>;

public class RejectRmaCommandValidator : AbstractValidator<RejectRmaCommand>
{
    public RejectRmaCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public class RejectRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<RejectRmaCommand, RmaDto>
{
    public RejectRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(RejectRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Reject);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);

        rma.ResolutionNotes = request.Reason.Trim();
        rma.Resolution = RmaResolution.None;
        RmaWorkflow.Transition(Db, rma, RmaStatus.Rejected, Actor, request.Reason.Trim(), Clock.UtcNow);
        await SaveAsync(ct);

        await RmaSupport.SendCustomerAsync(Templates, "return.rejected", order, rma,
            new Dictionary<string, string> { ["rejectionReason"] = RmaSupport.Encode(rma.ResolutionNotes) }, ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Staff cancel a return before anything was shipped (e.g. the customer phoned to withdraw it).</summary>
public record CancelRmaCommand(Guid RmaId, string? Note = null) : IRequest<RmaDto>;

public class CancelRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<CancelRmaCommand, RmaDto>
{
    public CancelRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(CancelRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Cancel);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        RmaWorkflow.Transition(Db, rma, RmaStatus.Cancelled, Actor,
            (request.Note ?? "Cancelled by staff.") + RmaLabels.VoidReminder(rma), Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Asks the customer for more information; the return stays Requested (RT-33).</summary>
public record RequestRmaInfoCommand(Guid RmaId, string Message) : IRequest<RmaDto>;

public class RequestRmaInfoCommandValidator : AbstractValidator<RequestRmaInfoCommand>
{
    public RequestRmaInfoCommandValidator() => RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
}

public class RequestRmaInfoCommandHandler : RmaAdminHandlerBase, IRequestHandler<RequestRmaInfoCommand, RmaDto>
{
    public RequestRmaInfoCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(RequestRmaInfoCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.RequestInfo);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var now = Clock.UtcNow;

        RmaMessaging.Add(Db, rma, Actor, request.Message, isInternal: false, now);
        RmaWorkflow.Note(Db, rma, Actor, "Asked the customer for more information.", now);
        await SaveAsync(ct);

        await RmaSupport.SendCustomerAsync(Templates, "return.more-info", order, rma,
            new Dictionary<string, string> { ["adminMessage"] = RmaSupport.Encode(request.Message.Trim()) }, ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Adds a thread message; internal notes are never shown or emailed to the customer (RT-35).</summary>
public record AddRmaMessageCommand(Guid RmaId, string Body, bool IsInternal = true, bool NotifyCustomer = false) : IRequest<RmaDto>;

public class AddRmaMessageCommandValidator : AbstractValidator<AddRmaMessageCommand>
{
    public AddRmaMessageCommandValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
}

public class AddRmaMessageCommandHandler : RmaAdminHandlerBase, IRequestHandler<AddRmaMessageCommand, RmaDto>
{
    public AddRmaMessageCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(AddRmaMessageCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        RmaMessaging.Add(Db, rma, Actor, request.Body, request.IsInternal, Clock.UtcNow);
        await SaveAsync(ct);

        if (!request.IsInternal && request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(Templates, "return.more-info", order, rma,
                new Dictionary<string, string> { ["adminMessage"] = RmaSupport.Encode(request.Body.Trim()) }, ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Records the returning dealer for a firearm return (RT-14).</summary>
public record SetRmaFflDetailsCommand(Guid RmaId, string DealerName, string FflNumber, DateTime FflExpiry) : IRequest<RmaDto>;

public class SetRmaFflDetailsCommandValidator : AbstractValidator<SetRmaFflDetailsCommand>
{
    public SetRmaFflDetailsCommandValidator()
    {
        RuleFor(x => x.DealerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FflNumber).NotEmpty().MaximumLength(50);
    }
}

public class SetRmaFflDetailsCommandHandler : RmaAdminHandlerBase, IRequestHandler<SetRmaFflDetailsCommand, RmaDto>
{
    public SetRmaFflDetailsCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(SetRmaFflDetailsCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.SetFfl);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        rma.FflDealerName = request.DealerName.Trim();
        rma.FflNumber = request.FflNumber.Trim();
        rma.FflExpiry = request.FflExpiry.Date;
        RmaWorkflow.Note(Db, rma, Actor, $"Returning dealer recorded: {rma.FflDealerName} (FFL {rma.FflNumber}, expires {rma.FflExpiry:yyyy-MM-dd}).", Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Changes the requested quantities until the goods arrive (RT-34). Quantity 0 removes a line.</summary>
public record UpdateRmaLinesCommand(Guid RmaId, List<RmaLineQuantity> Lines, string? Note = null) : IRequest<RmaDto>;

public record RmaLineQuantity(Guid RmaLineItemId, int Quantity);

public class UpdateRmaLinesCommandHandler : RmaAdminHandlerBase, IRequestHandler<UpdateRmaLinesCommand, RmaDto>
{
    public UpdateRmaLinesCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(UpdateRmaLinesCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.EditLines);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var claimed = await ReturnEligibility.ClaimedQuantitiesAsync(Db, order.Id, rma.Id, ct);

        var changes = new List<string>();
        foreach (var change in request.Lines)
        {
            var line = rma.Lines.FirstOrDefault(l => l.Id == change.RmaLineItemId)
                ?? throw new NotFoundException(nameof(RmaLineItem), change.RmaLineItemId);
            var orderLine = order.Lines.First(l => l.Id == line.OrderLineItemId);
            var max = orderLine.Quantity - claimed.GetValueOrDefault(orderLine.Id);
            if (change.Quantity < 0 || change.Quantity > max)
                throw new ValidationException(new[] { new ValidationFailure("Lines", $"'{line.ProductName}': quantity must be 0 to {max}.") });
            if (change.Quantity == line.Quantity) continue;
            changes.Add($"{line.ProductName}: {line.Quantity} → {change.Quantity}");
            line.Quantity = change.Quantity;
        }

        var removed = rma.Lines.Where(l => l.Quantity == 0).ToList();
        if (removed.Count == rma.Lines.Count)
            throw new ConflictException("A return must keep at least one item. Cancel or reject it instead.");
        foreach (var r in removed)
            Db.RmaLineItems.Remove(r);

        if (changes.Count > 0)
            RmaWorkflow.Note(Db, rma, Actor, "Items edited: " + string.Join("; ", changes) + (string.IsNullOrWhiteSpace(request.Note) ? "" : $" ({request.Note.Trim()})"), Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

// =====================================================================================================
// Shipping
// =====================================================================================================

/// <summary>
/// Records the return label / shipping details (RT-41 / RT-42): carrier, tracking, a link to the label PDF
/// and its cost (deducted from the refund under the "prepaid, deducted" policy), plus instructions. The label
/// is created in the carrier's portal for now — see <see cref="RmaLabels"/>.
/// </summary>
public record SetRmaLabelCommand(
    Guid RmaId,
    string? Carrier,
    string? Service,
    string? TrackingNumber,
    string? LabelUrl,
    decimal? LabelCost,
    string? Instructions,
    bool NotifyCustomer = true) : IRequest<RmaDto>;

public class SetRmaLabelCommandValidator : AbstractValidator<SetRmaLabelCommand>
{
    public SetRmaLabelCommandValidator()
    {
        RuleFor(x => x.Carrier).MaximumLength(50);
        RuleFor(x => x.Service).MaximumLength(100);
        RuleFor(x => x.TrackingNumber).MaximumLength(100);
        RuleFor(x => x.LabelUrl).MaximumLength(1000)
            .Must(u => u is null || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("The label link must start with http:// or https://.");
        RuleFor(x => x.LabelCost).GreaterThanOrEqualTo(0).When(x => x.LabelCost.HasValue);
        RuleFor(x => x.Instructions).MaximumLength(2000);
    }
}

public class SetRmaLabelCommandHandler : RmaAdminHandlerBase, IRequestHandler<SetRmaLabelCommand, RmaDto>
{
    public SetRmaLabelCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(SetRmaLabelCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.SetLabel);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var settings = await ReturnSettingsStore.LoadAsync(SettingsService, ct);

        rma.LabelCarrier = Clean(request.Carrier) ?? settings.LabelCarrier;
        rma.LabelService = Clean(request.Service);
        rma.TrackingNumber = Clean(request.TrackingNumber);
        rma.LabelUrl = Clean(request.LabelUrl);
        rma.LabelCost = request.LabelCost;
        if (request.Instructions is not null)
            rma.ReturnInstructions = Clean(request.Instructions);
        if (rma.LabelUrl is not null && rma.ReturnMethod == RmaReturnMethod.SelfShip)
            rma.ReturnMethod = RmaReturnMethod.PrepaidLabel;

        RmaWorkflow.Note(Db, rma, Actor,
            $"Return shipping details saved ({rma.LabelCarrier}{(rma.TrackingNumber is null ? "" : " " + rma.TrackingNumber)}).", Clock.UtcNow);
        await SaveAsync(ct);

        if (request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(Templates, "return.label-ready", order, rma, RmaApproval.ApprovalVars(settings, rma), ct);
        return await DtoAsync(rma, order, ct);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>Marks an approved return as shipped back (first carrier scan / customer says it's sent).</summary>
public record MarkRmaInTransitCommand(Guid RmaId, string? Carrier = null, string? TrackingNumber = null) : IRequest<RmaDto>;

public class MarkRmaInTransitCommandHandler : RmaAdminHandlerBase, IRequestHandler<MarkRmaInTransitCommand, RmaDto>
{
    public MarkRmaInTransitCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(MarkRmaInTransitCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.MarkInTransit);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        if (!string.IsNullOrWhiteSpace(request.Carrier)) rma.LabelCarrier = request.Carrier.Trim();
        if (!string.IsNullOrWhiteSpace(request.TrackingNumber)) rma.TrackingNumber = request.TrackingNumber.Trim();
        RmaWorkflow.Transition(Db, rma, RmaStatus.InTransit, Actor, null, Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

// =====================================================================================================
// Receiving and inspection
// =====================================================================================================

/// <summary>One line on the receive screen (RT-50 / RT-51).</summary>
public record RmaReceiveLine(Guid RmaLineItemId, int QuantityReceived, RmaReceivedCondition Condition, RmaDisposition Disposition);

/// <summary>
/// Records what arrived (RT-50) and restocks it (RT-51 / RT-52): Restock / Restock-as-open-box put the units back
/// into the order's store (or the default restock store), each change written to the inventory audit log. Can be
/// repeated for a partial or corrected receipt (RT-05) — only the difference is restocked or reversed.
/// </summary>
public record ReceiveRmaCommand(Guid RmaId, List<RmaReceiveLine> Lines, string? Note = null, bool NotifyCustomer = true) : IRequest<RmaDto>;

public class ReceiveRmaCommandValidator : AbstractValidator<ReceiveRmaCommand>
{
    public ReceiveRmaCommandValidator()
    {
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.QuantityReceived).InclusiveBetween(0, 100000));
        RuleFor(x => x.Note).MaximumLength(2000);
    }
}

public class ReceiveRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<ReceiveRmaCommand, RmaDto>
{
    private readonly IInventoryService _inventory;

    public ReceiveRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock, IInventoryService inventory)
        : base(db, current, settings, templates, clock) => _inventory = inventory;

    public async Task<RmaDto> Handle(ReceiveRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Receive);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var settings = await ReturnSettingsStore.LoadAsync(SettingsService, ct);
        var now = Clock.UtcNow;
        var actor = Actor;
        var firstReceipt = rma.Status != RmaStatus.Received;

        // Lock in the status before stock moves, so a concurrent receive loses on the row version (RT-67).
        if (firstReceipt)
        {
            var skipped = rma.Status == RmaStatus.Requested ? " (review and shipping steps skipped — counter return)" : "";
            RmaWorkflow.Transition(Db, rma, RmaStatus.Received, actor, (request.Note ?? "Package received.") + skipped, now);
        }
        await SaveAsync(ct);

        var storeId = order.AssignedStoreId ?? settings.DefaultRestockStoreId;
        var notes = new List<string>();

        foreach (var input in request.Lines)
        {
            var line = rma.Lines.FirstOrDefault(l => l.Id == input.RmaLineItemId)
                ?? throw new NotFoundException(nameof(RmaLineItem), input.RmaLineItemId);

            line.QuantityReceived = input.QuantityReceived;
            line.ConditionReceived = input.QuantityReceived == 0 ? RmaReceivedCondition.NotReceived : input.Condition;
            line.Disposition = input.Disposition;
            // RT-55: wrong item / not received → nothing accepted by default; more than authorised is held for staff.
            line.QuantityAccepted = line.ConditionReceived is RmaReceivedCondition.WrongItem or RmaReceivedCondition.NotReceived
                ? 0
                : Math.Min(line.QuantityReceived, line.Quantity);
            if (input.QuantityReceived > line.Quantity)
                notes.Add($"{line.ProductName}: {input.QuantityReceived} received but only {line.Quantity} authorised — extra units held for review.");

            // Only resellable units go back on the shelf; open-box restocks the same SKU (no open-box SKU mapping yet).
            var restockable = line.ConditionReceived is RmaReceivedCondition.Resellable or RmaReceivedCondition.OpenBox
                              && input.Disposition is RmaDisposition.Restock or RmaDisposition.RestockOpenBox;
            var target = restockable ? line.QuantityAccepted : 0;
            var delta = target - line.QuantityRestocked;
            if (delta == 0) continue;

            if (storeId is not Guid sid)
            {
                notes.Add($"{line.ProductName}: not restocked — the order has no store and no default restock store is set.");
                continue;
            }

            var productExists = await Db.Products.AnyAsync(p => p.Id == line.ProductId, ct);
            if (!productExists)
            {
                notes.Add($"{line.ProductName}: not restocked — the product no longer exists.");
                continue;
            }

            var newQty = await _inventory.AdjustStockAsync(line.ProductId, line.ProductVariantId, sid, delta, ct);
            line.QuantityRestocked = target;
            Db.InventoryAuditLogs.Add(new InventoryAuditLog
            {
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                StoreId = sid,
                // The audit table only holds non-negative quantities (backordered stock reads as 0).
                OldQty = Math.Max(0, newQty - delta),
                NewQty = Math.Max(0, newQty),
                Action = InventoryAuditAction.ReturnRestock,
                RmaId = rma.Id,
                Notes = WorkflowNote($"{rma.RmaNumber}: {(delta > 0 ? "restocked" : "restock reversed")} {Math.Abs(delta)}"),
                Source = InventoryScanSource.Manual,
                ClientIp = Current.IpAddress,
            });
            notes.Add($"{line.ProductName}: {(delta > 0 ? "restocked" : "restock reversed")} {Math.Abs(delta)} (stock now {newQty}).");
        }

        RmaWorkflow.Note(Db, rma, actor, "Receipt recorded. " + string.Join(" ", notes), now);
        await SaveAsync(ct);

        if (firstReceipt && request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(Templates, "return.received", order, rma, new Dictionary<string, string>
            {
                ["receivedItems"] = RmaSupport.ItemsHtml(rma.Lines.Select(l => (l.ProductName, l.Sku, l.QuantityReceived))),
            }, ct);
        return await DtoAsync(rma, order, ct);
    }

    private static string WorkflowNote(string s) => s.Length <= 500 ? s : s[..500];
}

/// <summary>One line of the inspection (RT-54): accepted quantity and an optional deduction with a reason.</summary>
public record RmaInspectLine(Guid RmaLineItemId, int QuantityAccepted, decimal Deduction = 0, string? DeductionReason = null);

/// <summary>
/// Inspection: fixes the accepted quantities and deductions, the shipping / fee choices and any manual
/// adjustment (RT-59), then freezes the refund breakdown. Can be repeated until the refund is sent.
/// </summary>
public record InspectRmaCommand(
    Guid RmaId,
    List<RmaInspectLine> Lines,
    bool RefundShipping = false,
    bool WaiveRestockFee = false,
    decimal Adjustment = 0,
    string? AdjustmentReason = null,
    string? Note = null) : IRequest<RmaDto>;

public class InspectRmaCommandValidator : AbstractValidator<InspectRmaCommand>
{
    public InspectRmaCommandValidator()
    {
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.QuantityAccepted).GreaterThanOrEqualTo(0);
            l.RuleFor(i => i.Deduction).GreaterThanOrEqualTo(0);
            l.RuleFor(i => i.DeductionReason).NotEmpty().When(i => i.Deduction > 0).WithMessage("Give a reason for every deduction.");
            l.RuleFor(i => i.DeductionReason).MaximumLength(500);
        });
        RuleFor(x => x.AdjustmentReason).NotEmpty().When(x => x.Adjustment != 0).WithMessage("Give a reason for the adjustment.");
        RuleFor(x => x.AdjustmentReason).MaximumLength(500);
    }
}

public class InspectRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<InspectRmaCommand, RmaDto>
{
    public InspectRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(InspectRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Inspect);
        if (rma.RefundStartedOnUtc is not null && Clock.UtcNow - rma.RefundStartedOnUtc < TimeSpan.FromMinutes(5))
            throw new ConflictException("A refund for this return is being processed; wait for it to finish.");
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var settings = await ReturnSettingsStore.LoadAsync(SettingsService, ct);

        foreach (var input in request.Lines)
        {
            var line = rma.Lines.FirstOrDefault(l => l.Id == input.RmaLineItemId)
                ?? throw new NotFoundException(nameof(RmaLineItem), input.RmaLineItemId);
            if (input.QuantityAccepted > line.QuantityReceived)
                throw new ValidationException(new[] { new ValidationFailure("Lines", $"'{line.ProductName}': cannot accept more than the {line.QuantityReceived} received.") });
            if (input.QuantityAccepted > line.Quantity)
                throw new ValidationException(new[] { new ValidationFailure("Lines", $"'{line.ProductName}': cannot accept more than the {line.Quantity} authorised.") });
            if (input.QuantityAccepted < line.QuantityRestocked)
                throw new ValidationException(new[] { new ValidationFailure("Lines", $"'{line.ProductName}': {line.QuantityRestocked} were already restocked — correct the receipt first.") });
            line.QuantityAccepted = input.QuantityAccepted;
            line.Deduction = input.Deduction;
            line.DeductionReason = string.IsNullOrWhiteSpace(input.DeductionReason) ? null : input.DeductionReason.Trim();
        }

        rma.RefundShipping = request.RefundShipping;
        rma.WaiveRestockFee = request.WaiveRestockFee;
        rma.Adjustment = request.Adjustment;
        rma.AdjustmentReason = request.Adjustment == 0 ? null : request.AdjustmentReason?.Trim();

        var now = Clock.UtcNow;
        if (rma.Status != RmaStatus.Inspected)
            RmaWorkflow.Transition(Db, rma, RmaStatus.Inspected, Actor, request.Note, now);

        var breakdown = await RmaSupport.BreakdownAsync(Db, rma, order, settings, persist: true, ct);
        // Money already paid out on this return can't be taken back by re-inspecting (reopened returns).
        if (rma.RefundedAmount is decimal paid && paid > 0 && breakdown.Total < paid)
            throw new ConflictException(
                $"{RmaSupport.Money(paid, order.CurrencyCode)} was already refunded on this return; the new total cannot be lower.");
        RmaRefundCalculator.Apply(rma, breakdown);

        var refundable = (await RmaSupport.RefundablePaymentsAsync(Db, order.Id, ct)).Sum(p => p.Amount - p.RefundedAmount);
        RmaWorkflow.Note(Db, rma, Actor,
            $"Inspection saved. Refund {RmaSupport.Money(rma.TotalRefund, order.CurrencyCode)}"
            + (rma.Adjustment != 0 ? $" incl. adjustment {RmaSupport.Money(rma.Adjustment, order.CurrencyCode)} ({rma.AdjustmentReason})" : "")
            + (rma.TotalRefund > refundable ? $". Warning: only {RmaSupport.Money(refundable, order.CurrencyCode)} is refundable to the original payment." : "."),
            now);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

// =====================================================================================================
// Settlement
// =====================================================================================================

/// <summary>
/// Sends the inspected refund (section 10.2). <see cref="RmaRefundMethod.OriginalPayment"/> refunds the order's
/// captured payments — gateway first, then gift certificate (RT-68) — through the payment module, which only records a
/// refund once the gateway accepts it (RT-61); a failure leaves the return Inspected with the gateway message and
/// allows a retry (only the unpaid remainder is retried). <see cref="RmaRefundMethod.StoreCredit"/> issues the total
/// (plus the configured bonus, RT-69) to the customer's store-credit balance. <see cref="RmaRefundMethod.Manual"/>
/// records a refund made outside the system with a reference.
/// </summary>
public record RefundRmaCommand(Guid RmaId, RmaRefundMethod Method, string? ManualReference = null, bool NotifyCustomer = true) : IRequest<RmaDto>;

public class RefundRmaCommandValidator : AbstractValidator<RefundRmaCommand>
{
    public RefundRmaCommandValidator()
    {
        RuleFor(x => x.Method).NotEqual(RmaRefundMethod.None);
        RuleFor(x => x.ManualReference).NotEmpty().When(x => x.Method == RmaRefundMethod.Manual)
            .WithMessage("Enter how and where the refund was made (cheque number, gateway portal reference …).");
        RuleFor(x => x.ManualReference).MaximumLength(500);
    }
}

public class RefundRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<RefundRmaCommand, RmaDto>
{
    /// <summary>A refund marked as running longer than this is treated as abandoned (process crash) and may be retried.</summary>
    private static readonly TimeSpan StaleRefund = TimeSpan.FromMinutes(5);

    private readonly ISender _mediator;
    private readonly IStoreCreditService _storeCredit;
    private readonly IRewardPointsService _points;

    public RefundRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock,
        ISender mediator, IStoreCreditService storeCredit, IRewardPointsService points)
        : base(db, current, settings, templates, clock)
    {
        _mediator = mediator;
        _storeCredit = storeCredit;
        _points = points;
    }

    public async Task<RmaDto> Handle(RefundRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, request.Method == RmaRefundMethod.StoreCredit ? RmaActions.StoreCredit : RmaActions.Refund);
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var settings = await ReturnSettingsStore.LoadAsync(SettingsService, ct);
        var now = Clock.UtcNow;
        var actor = Actor;

        var alreadyRefunded = rma.RefundedAmount ?? 0m;
        var remaining = rma.TotalRefund - alreadyRefunded;
        if (remaining <= 0 && rma.TotalRefund > 0)
            throw new ConflictException("This return has already been refunded in full.");
        if (alreadyRefunded > 0 && request.Method != RmaRefundMethod.OriginalPayment && request.Method != RmaRefundMethod.Manual)
            throw new ConflictException("Part of this refund already went to the original payment; finish it the same way or record the rest as a manual refund.");

        // RT-67: one refund at a time per return. The row version makes the claim race-safe.
        if (rma.RefundStartedOnUtc is DateTime started && now - started < StaleRefund)
            throw new ConflictException("A refund for this return is already being processed.");
        rma.RefundStartedOnUtc = now;
        rma.RefundAttempts++;
        await SaveAsync(ct);

        string methodText;
        try
        {
            switch (request.Method)
            {
                case RmaRefundMethod.OriginalPayment:
                    methodText = await RefundToPaymentsAsync(rma, order, remaining, settings, ct);
                    break;
                case RmaRefundMethod.StoreCredit:
                    methodText = await IssueStoreCreditAsync(rma, order, remaining, settings, ct);
                    break;
                default:
                    rma.RefundedAmount = rma.TotalRefund;
                    rma.RefundReference = request.ManualReference!.Trim();
                    methodText = $"a manual refund ({rma.RefundReference})";
                    break;
            }
        }
        catch (Exception ex) when (ex is ConflictException or ValidationException or NotFoundException or ForbiddenAccessException)
        {
            // RT-61: the return stays Inspected with the reason; whatever part already went through is kept.
            rma.RefundStartedOnUtc = null;
            rma.LastRefundError = RmaWorkflow.Truncate(ex.Message, 1000);
            RmaWorkflow.Note(Db, rma, actor, $"Refund attempt {rma.RefundAttempts} failed: {ex.Message}", now);
            await SaveAsync(ct);
            await RmaSupport.SendStaffAsync(Templates, settings, order, rma, "Refund failed", ex.Message, ct);
            throw;
        }

        rma.RefundStartedOnUtc = null;
        rma.LastRefundError = null;
        rma.RefundMethod = request.Method;
        rma.Resolution = request.Method == RmaRefundMethod.StoreCredit ? RmaResolution.StoreCredit : RmaResolution.Refund;
        var amountText = request.Method == RmaRefundMethod.StoreCredit
            ? RmaSupport.Money(rma.StoreCreditIssued ?? 0m, order.CurrencyCode)
            : RmaSupport.Money(rma.TotalRefund, order.CurrencyCode);
        RmaWorkflow.Transition(Db, rma, RmaStatus.Refunded, actor, $"Refunded {amountText} as {methodText}.", now);
        await SaveAsync(ct);

        // Loyalty points earned by the order are clawed back once its payment is fully refunded.
        if (order.CustomerId is Guid cid && order.PaymentStatus is PaymentStatus.Refunded)
            await _points.DeductForRefundAsync(cid, order.Id, ct);

        if (request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(Templates, "return.refunded", order, rma, new Dictionary<string, string>
            {
                ["refundAmount"] = amountText,
                ["refundMethod"] = RmaSupport.Encode(methodText),
                ["refundBreakdown"] = BreakdownHtml(rma, order.CurrencyCode),
                ["inspectionNotes"] = RmaSupport.Encode(string.Join(" ", rma.Lines.Where(l => l.DeductionReason != null).Select(l => $"{l.ProductName}: {l.DeductionReason}."))),
            }, ct);

        return await DtoAsync(rma, order, ct);
    }

    private async Task<string> RefundToPaymentsAsync(Domain.Entities.Rma rma, Order order, decimal remaining, ReturnSettingsDto settings, CancellationToken ct)
    {
        if (remaining <= 0)
        {
            rma.RefundedAmount = rma.TotalRefund;
            return "no payment (nothing to refund)";
        }

        var payments = await RmaSupport.RefundablePaymentsAsync(Db, order.Id, ct);
        var auto = payments.Where(p => RmaSupport.IsAutoRefundable(p.Method)).ToList();
        var available = auto.Sum(p => p.Amount - p.RefundedAmount);
        if (available < remaining)
            throw new ConflictException(
                $"Only {RmaSupport.Money(available, order.CurrencyCode)} can be refunded automatically to this order's payments " +
                $"(needed {RmaSupport.Money(remaining, order.CurrencyCode)}). Refund the rest outside the system and record it as a manual refund, or issue store credit.");

        var parts = new List<string>();
        foreach (var payment in auto)
        {
            if (remaining <= 0) break;
            var part = Math.Min(remaining, payment.Amount - payment.RefundedAmount);
            // Throws (after recording the failed attempt on the payment) when the gateway declines (AN-27).
            await _mediator.Send(new Payments.RefundPaymentCommand(payment.Id, part, $"Return {rma.RmaNumber}"), ct);
            remaining -= part;
            rma.RefundedAmount = (rma.RefundedAmount ?? 0m) + part;
            parts.Add($"{payment.Method} {RmaSupport.Money(part, order.CurrencyCode)}");
            rma.RefundReference = RmaWorkflow.Truncate(string.Join("; ", parts), 500);
            // Persist each successful part so a later failure never re-refunds it.
            await SaveAsync(ct);
        }
        return "a refund to the original payment (" + string.Join(", ", parts) + ")";
    }

    private async Task<string> IssueStoreCreditAsync(Domain.Entities.Rma rma, Order order, decimal remaining, ReturnSettingsDto settings, CancellationToken ct)
    {
        if (rma.CustomerId is not Guid customerId)
            throw new ConflictException("Store credit needs a customer account; this order was placed as a guest.");
        if (remaining <= 0)
            throw new ConflictException("There is nothing to issue as store credit.");

        var bonus = Math.Round(remaining * settings.StoreCreditBonusPercent / 100m, 2, MidpointRounding.AwayFromZero);
        var amount = remaining + bonus;
        await _storeCredit.IssueAsync(customerId, amount, order.CurrencyCode,
            $"Store credit for return {rma.RmaNumber}" + (bonus > 0 ? $" (incl. {RmaSupport.Money(bonus, order.CurrencyCode)} bonus)" : ""),
            rma.Id, order.Id, ct);
        rma.RefundedAmount = rma.TotalRefund;
        rma.StoreCreditIssued = amount;
        rma.RefundReference = bonus > 0 ? $"Store credit incl. bonus {bonus:0.00}" : "Store credit";
        return "store credit" + (bonus > 0 ? $" (including a {settings.StoreCreditBonusPercent:0.##}% bonus)" : "");
    }

    private static string BreakdownHtml(Domain.Entities.Rma r, string cur)
    {
        var rows = new List<(string, decimal)> { ("Items", r.SubtotalRefund) };
        if (r.TaxRefund != 0) rows.Add(("Tax", r.TaxRefund));
        if (r.ShippingRefund != 0) rows.Add(("Shipping", r.ShippingRefund));
        if (r.RestockFee != 0) rows.Add(("Restocking fee", -r.RestockFee));
        if (r.LabelDeduction != 0) rows.Add(("Return label", -r.LabelDeduction));
        if (r.Adjustment != 0) rows.Add(("Adjustment", r.Adjustment));
        rows.Add(("Total", r.TotalRefund));
        return "<table>" + string.Concat(rows.Select(x => $"<tr><td>{RmaSupport.Encode(x.Item1)}</td><td style=\"text-align:right\">{RmaSupport.Money(x.Item2, cur)}</td></tr>")) + "</table>";
    }
}

/// <summary>
/// Exchange (RT-75): creates a no-charge replacement order for the accepted units (same items), fulfilled from
/// the original store, and takes the replacement stock. A price difference is not handled here — refund or charge
/// it separately.
/// </summary>
public record ExchangeRmaCommand(Guid RmaId, string? Note = null, bool NotifyCustomer = true) : IRequest<RmaDto>;

public class ExchangeRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<ExchangeRmaCommand, RmaDto>
{
    private readonly IInventoryService _inventory;

    public ExchangeRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock, IInventoryService inventory)
        : base(db, current, settings, templates, clock) => _inventory = inventory;

    public async Task<RmaDto> Handle(ExchangeRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Exchange);
        if (rma.RefundedAmount is > 0)
            throw new ConflictException("This return was already (partly) refunded and cannot also be exchanged.");
        if (rma.Lines.All(l => l.QuantityAccepted <= 0))
            throw new ConflictException("Nothing was accepted at inspection, so there is nothing to replace.");

        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var now = Clock.UtcNow;

        var seq = await Db.Orders.IgnoreQueryFilters().CountAsync(o => o.OrderNumber.StartsWith(order.OrderNumber + "-R"), ct) + 1;
        var customisations = order.Lines.ToDictionary(l => l.Id, l => l.CustomAttributesJson);
        var replacement = new Order
        {
            OrderNumber = $"{order.OrderNumber}-R{seq}",
            CustomerId = order.CustomerId,
            Status = OrderStatus.Processing,
            PaymentStatus = PaymentStatus.Captured, // no charge: the original order already paid
            ShippingAddressId = order.ShippingAddressId,
            AssignedStoreId = order.AssignedStoreId,
            IsPickup = order.IsPickup,
            CurrencyCode = order.CurrencyCode,
            ShippingMethodId = order.ShippingMethodId,
            ShippingMethodName = order.ShippingMethodName,
            ShippingCarrier = order.ShippingCarrier,
            PlacedOnUtc = now,
            RoutedOnUtc = order.AssignedStoreId is null ? null : now,
            InternalNotes = $"Replacement for return {rma.RmaNumber}.",
        };
        foreach (var line in rma.Lines.Where(l => l.QuantityAccepted > 0))
        {
            replacement.Lines.Add(new OrderLineItem
            {
                OrderId = replacement.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = line.QuantityAccepted,
                CustomAttributesJson = customisations.GetValueOrDefault(line.OrderLineItemId),
            });
        }
        Db.Orders.Add(replacement);

        rma.ReplacementOrderId = replacement.Id;
        rma.Resolution = RmaResolution.Replacement;
        RmaWorkflow.Transition(Db, rma, RmaStatus.Exchanged, Actor,
            $"Replacement order {replacement.OrderNumber} created." + (string.IsNullOrWhiteSpace(request.Note) ? "" : " " + request.Note.Trim()), now);
        await SaveAsync(ct);

        // Take the replacement units out of stock at the fulfilling store (RT-77).
        await _inventory.DecrementForOrderAsync(replacement, ct);

        if (request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(Templates, "return.exchanged", order, rma,
                new Dictionary<string, string> { ["replacementOrderNumber"] = RmaSupport.Encode(replacement.OrderNumber) }, ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Closes a return (all settled, or the remainder written off — RT-05 / RT-09).</summary>
public record CloseRmaCommand(Guid RmaId, string? Note = null) : IRequest<RmaDto>;

public class CloseRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<CloseRmaCommand, RmaDto>
{
    public CloseRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(CloseRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Close);
        if (rma.RefundStartedOnUtc is not null && Clock.UtcNow - rma.RefundStartedOnUtc < TimeSpan.FromMinutes(5))
            throw new ConflictException("A refund for this return is still being processed.");
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        var unsettled = rma.Status is RmaStatus.Received or RmaStatus.Inspected;
        RmaWorkflow.Transition(Db, rma, RmaStatus.Closed, Actor,
            request.Note ?? (unsettled ? "Closed without a refund or exchange." : null), Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

/// <summary>Reopens a closed return back to Inspected (managers) so a remaining amount can be settled.</summary>
public record ReopenRmaCommand(Guid RmaId, string Reason) : IRequest<RmaDto>;

public class ReopenRmaCommandValidator : AbstractValidator<ReopenRmaCommand>
{
    public ReopenRmaCommandValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
}

public class ReopenRmaCommandHandler : RmaAdminHandlerBase, IRequestHandler<ReopenRmaCommand, RmaDto>
{
    public ReopenRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(ReopenRmaCommand request, CancellationToken ct)
    {
        var rma = await RmaSupport.LoadAsync(Db, request.RmaId, ct);
        Require(rma, RmaActions.Reopen);
        if (rma.Lines.All(l => l.QuantityReceived == 0) && rma.ReceivedOnUtc is null)
            throw new ConflictException("Nothing was received on this return, so there is nothing to reopen.");
        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct);
        RmaWorkflow.Transition(Db, rma, RmaStatus.Inspected, Actor, "Reopened: " + request.Reason.Trim(), Clock.UtcNow);
        await SaveAsync(ct);
        return await DtoAsync(rma, order, ct);
    }
}

// =====================================================================================================
// Helpers
// =====================================================================================================

internal static class RmaMessaging
{
    public static RmaMessage Add(IApplicationDbContext db, Domain.Entities.Rma rma, RmaActor actor, string body, bool isInternal, DateTime now)
    {
        var message = new RmaMessage
        {
            RmaId = rma.Id,
            AuthorType = actor.Type,
            AuthorId = actor.Id,
            AuthorName = RmaWorkflow.Truncate(actor.Name, 256),
            Body = RmaWorkflow.Truncate(body.Trim(), 4000)!,
            IsInternal = isInternal,
            CreatedOnUtc = now,
        };
        db.RmaMessages.Add(message);
        rma.Messages.Add(message);
        return message;
    }
}

/// <summary>Return label notes. Carrier-generated return labels are not wired to the carrier APIs yet (RT-41).</summary>
internal static class RmaLabels
{
    /// <summary>RT-43: reminder to void an unused label in the carrier portal (no carrier void API is called yet).</summary>
    public static string VoidReminder(Domain.Entities.Rma rma)
        => string.IsNullOrWhiteSpace(rma.TrackingNumber) && string.IsNullOrWhiteSpace(rma.LabelUrl)
            ? string.Empty
            : $" Void the unused {rma.LabelCarrier ?? "carrier"} label {rma.TrackingNumber} in the carrier portal.";
}
