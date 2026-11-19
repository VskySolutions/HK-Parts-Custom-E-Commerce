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

/// <summary>One order line on a return request (section 6.2).</summary>
public record RmaRequestLine(
    Guid OrderLineItemId,
    int Quantity,
    Guid? ReasonId = null,
    string? Comment = null,
    RmaItemCondition? Condition = null);

/// <summary>A file posted with a request or added later (photos of damage, the shipping label …).</summary>
public record RmaUpload(string FileName, string ContentType, long Length, Stream Content);

/// <summary>
/// A signed-in customer requests a return on one of their orders (RT-20 / RT-24). Lines are checked against
/// <see cref="ReturnEligibility"/>; reasons may require a comment or photos; qualifying requests are approved
/// automatically (RT-25). <see cref="Photos"/> is filled by the controller from the multipart body.
/// </summary>
public record SubmitRmaCommand(
    Guid OrderId,
    List<RmaRequestLine> Lines,
    RmaResolution RequestedResolution = RmaResolution.Refund,
    RmaReturnMethod ReturnMethod = RmaReturnMethod.SelfShip,
    string? Comment = null,
    bool AcceptPolicy = false,
    IReadOnlyList<RmaUpload>? Photos = null) : IRequest<RmaDto>;

public class SubmitRmaCommandValidator : AbstractValidator<SubmitRmaCommand>
{
    public SubmitRmaCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Select at least one item to return.");
        RuleForEach(x => x.Lines).ChildRules(l =>
        {
            l.RuleFor(i => i.Quantity).GreaterThan(0);
            l.RuleFor(i => i.ReasonId).NotNull().WithMessage("Choose a reason for every item.");
            l.RuleFor(i => i.Comment).MaximumLength(1000);
        });
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.AcceptPolicy).Equal(true).WithMessage("Please accept the return policy.");
        RuleFor(x => x.RequestedResolution).NotEqual(RmaResolution.None);
        RuleFor(x => x.Photos!.Count).LessThanOrEqualTo(RmaFiles.MaxFilesPerRequest)
            .When(x => x.Photos is not null).WithMessage($"Attach at most {RmaFiles.MaxFilesPerRequest} photos.");
    }
}

public class SubmitRmaCommandHandler : IRequestHandler<SubmitRmaCommand, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEmailTemplateSender _templates;
    private readonly ISettingsService _settings;
    private readonly IPrivateFileStore _files;
    private readonly IDateTimeProvider _clock;

    public SubmitRmaCommandHandler(
        IApplicationDbContext db, ICurrentUserService current, IEmailTemplateSender templates,
        ISettingsService settings, IPrivateFileStore files, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _templates = templates;
        _settings = settings;
        _files = files;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(SubmitRmaCommand request, CancellationToken cancellationToken)
    {
        var customer = await RmaSupport.CurrentCustomerAsync(_db, _current, cancellationToken);

        // Buyer scoping: a foreign order simply isn't found.
        var order = await _db.Orders.Include(o => o.Lines).Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId && o.CustomerId == customer.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        var settings = await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
        if (request.RequestedResolution == RmaResolution.StoreCredit && !settings.AllowStoreCredit)
            throw new ConflictException("Store credit is not offered for returns.");
        if (request.RequestedResolution == RmaResolution.Replacement && !settings.AllowExchange)
            throw new ConflictException("Exchanges are not offered for returns.");
        if (request.ReturnMethod == RmaReturnMethod.PrepaidLabel && settings.LabelMode is ReturnLabelMode.Disabled or ReturnLabelMode.CustomerShips)
            throw new ConflictException("Prepaid return labels are not offered.");
        if (request.ReturnMethod == RmaReturnMethod.DropOff && !settings.AllowDropOff)
            throw new ConflictException("Drop-off returns are not offered.");

        var now = _clock.UtcNow;
        var eligibility = await ReturnEligibility.EvaluateAsync(_db, order, settings, now, forStaff: false, null, cancellationToken);
        if (!eligibility.Eligible)
            throw new ConflictException(eligibility.Reason ?? "This order cannot be returned.");

        var actor = RmaActor.Customer(customer.Id, _current);
        var rma = await RmaFactory.BuildAsync(_db, order, eligibility, request.Lines, request.RequestedResolution, request.ReturnMethod,
            request.Comment, RmaSource.Customer, customer.Id, forStaff: false, now, cancellationToken);

        // Reasons that need photos (Damaged / Defective) when the policy asks for them.
        var photos = request.Photos ?? Array.Empty<RmaUpload>();
        if (settings.RequirePhotos && photos.Count == 0)
        {
            var reasonIds = rma.Lines.Where(l => l.ReasonId != null).Select(l => l.ReasonId!.Value).ToList();
            var needsPhoto = await _db.RmaReasons.AnyAsync(r => reasonIds.Contains(r.Id) && r.RequiresPhoto, cancellationToken);
            if (needsPhoto)
                throw new ValidationException(new[] { new ValidationFailure("Photos", "Please attach a photo of the damage or defect.") });
        }

        rma.RmaNumber = await RmaSupport.NextNumberAsync(_db, order, cancellationToken);
        _db.Rmas.Add(rma);
        RmaWorkflow.Note(_db, rma, actor, "Return requested by the customer.", now);

        foreach (var photo in photos)
            await RmaFiles.SaveAsync(_db, _files, rma, null, photo, RmaActorType.Customer, customer.Id, now, cancellationToken);

        // RT-25: auto-approve small, unrestricted requests whose every reason allows it.
        var autoApproved = false;
        if (settings.AutoApprove && !rma.RequiresFfl && !rma.ShipRestricted)
        {
            var reasonIds = rma.Lines.Where(l => l.ReasonId != null).Select(l => l.ReasonId!.Value).Distinct().ToList();
            var allAuto = await _db.RmaReasons.Where(r => reasonIds.Contains(r.Id)).CountAsync(r => r.AutoApprove, cancellationToken)
                          == reasonIds.Count && reasonIds.Count > 0;
            var estimate = await RmaSupport.BreakdownAsync(_db, rma, order, settings, persist: false, cancellationToken);
            if (allAuto && estimate.Total <= settings.AutoApproveMax)
            {
                RmaApproval.Approve(_db, rma, settings, RmaActor.System, "Approved automatically by the return policy.", now);
                autoApproved = true;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        await RmaSupport.SendCustomerAsync(_templates, "return.requested", order, rma, null, cancellationToken);
        if (autoApproved)
            await RmaApproval.NotifyApprovedAsync(_templates, settings, order, rma, cancellationToken);
        await RmaSupport.SendStaffAsync(_templates, settings, order, rma,
            autoApproved ? "New return (auto-approved)" : "New return request",
            rma.RequiresFfl ? "Contains a firearm: dealer details are needed before approval." : "A customer requested a return.",
            cancellationToken);

        return (await RmaSupport.ToDtoAsync(_db, _settings, rma, order, now, cancellationToken)).ForCustomer();
    }
}

/// <summary>
/// Staff create a return from the admin (phone / counter request, RT-03). Policy rules can be overridden with a
/// reason (RT-17); the quantity rule cannot. <paramref name="Approve"/> creates it already approved.
/// </summary>
public record CreateRmaCommand(
    Guid OrderId,
    List<RmaRequestLine> Lines,
    RmaResolution RequestedResolution = RmaResolution.Refund,
    RmaReturnMethod ReturnMethod = RmaReturnMethod.SelfShip,
    string? Comment = null,
    bool Approve = false,
    string? OverrideReason = null,
    bool NotifyCustomer = true) : IRequest<RmaDto>;

public class CreateRmaCommandValidator : AbstractValidator<CreateRmaCommand>
{
    public CreateRmaCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty();
        RuleForEach(x => x.Lines).ChildRules(l => l.RuleFor(i => i.Quantity).GreaterThan(0));
        RuleFor(x => x.Comment).MaximumLength(1000);
        RuleFor(x => x.OverrideReason).MaximumLength(500);
    }
}

public class CreateRmaCommandHandler : IRequestHandler<CreateRmaCommand, RmaDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _current;
    private readonly IEmailTemplateSender _templates;
    private readonly ISettingsService _settings;
    private readonly IDateTimeProvider _clock;

    public CreateRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, IEmailTemplateSender templates, ISettingsService settings, IDateTimeProvider clock)
    {
        _db = db;
        _current = current;
        _templates = templates;
        _settings = settings;
        _clock = clock;
    }

    public async Task<RmaDto> Handle(CreateRmaCommand request, CancellationToken cancellationToken)
    {
        var order = await RmaSupport.LoadOrderAsync(_db, request.OrderId, cancellationToken);
        var settings = await ReturnSettingsStore.LoadAsync(_settings, cancellationToken);
        var now = _clock.UtcNow;

        var eligibility = await ReturnEligibility.EvaluateAsync(_db, order, settings, now, forStaff: true, null, cancellationToken);
        if (!eligibility.Eligible)
            throw new ConflictException(eligibility.Reason ?? "This order cannot be returned.");

        // Anything the customer form would refuse needs a written override (RT-17).
        var customerView = await ReturnEligibility.EvaluateAsync(_db, order, settings, now, forStaff: false, null, cancellationToken);
        var overridden = request.Lines.Any(l => customerView.Lines.FirstOrDefault(c => c.OrderLineItemId == l.OrderLineItemId) is not { Eligible: true });
        if (overridden && string.IsNullOrWhiteSpace(request.OverrideReason))
            throw new ValidationException(new[] { new ValidationFailure(nameof(request.OverrideReason),
                $"This return is outside the policy ({customerView.Reason ?? "item not returnable"}). Enter an override reason.") });

        var actor = RmaActor.Admin(_current);
        var rma = await RmaFactory.BuildAsync(_db, order, eligibility, request.Lines, request.RequestedResolution, request.ReturnMethod,
            request.Comment, RmaSource.Admin, order.CustomerId, forStaff: true, now, cancellationToken);
        rma.CreatedByAdminId = _current.UserId;
        rma.RmaNumber = await RmaSupport.NextNumberAsync(_db, order, cancellationToken);
        if (overridden)
            rma.EligibilityOverrideReason = request.OverrideReason!.Trim();

        _db.Rmas.Add(rma);
        RmaWorkflow.Note(_db, rma, actor,
            overridden ? $"Created by staff (policy override: {rma.EligibilityOverrideReason})." : "Created by staff.", now);

        if (request.Approve)
        {
            if (rma.RequiresFfl)
                throw new ConflictException("Firearm returns need the returning dealer's FFL details before approval. Create the return, then add the dealer and approve.");
            RmaApproval.Approve(_db, rma, settings, actor, "Approved when created (skipped review).", now);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // NotifyCustomer is off for the quick return: the customer hears once, when the refund goes out.
        if (request.NotifyCustomer && request.Approve)
            await RmaApproval.NotifyApprovedAsync(_templates, settings, order, rma, cancellationToken);
        else if (request.NotifyCustomer)
            await RmaSupport.SendCustomerAsync(_templates, "return.requested", order, rma, null, cancellationToken);

        return await RmaSupport.ToDtoAsync(_db, _settings, rma, order, now, cancellationToken);
    }
}

/// <summary>Builds a new RMA and its lines from a request, enforcing the per-line rules.</summary>
internal static class RmaFactory
{
    public static async Task<Domain.Entities.Rma> BuildAsync(
        IApplicationDbContext db, Order order, ReturnEligibilityDto eligibility, IReadOnlyList<RmaRequestLine> inputs,
        RmaResolution resolution, RmaReturnMethod method, string? comment, RmaSource source, Guid? customerId,
        bool forStaff, DateTime now, CancellationToken ct)
    {
        if (inputs.GroupBy(i => i.OrderLineItemId).Any(g => g.Count() > 1))
            throw new ValidationException(new[] { new ValidationFailure("Lines", "Each item can appear only once on a return.") });

        var reasonIds = inputs.Where(i => i.ReasonId != null).Select(i => i.ReasonId!.Value).Distinct().ToList();
        var reasons = await db.RmaReasons.AsNoTracking().Where(r => reasonIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, ct);

        var rma = new Domain.Entities.Rma
        {
            OrderId = order.Id,
            CustomerId = customerId,
            Status = RmaStatus.Requested,
            RequestedResolution = resolution,
            ReturnMethod = method,
            Source = source,
            Reason = comment?.Trim() ?? string.Empty,
            RequestedOnUtc = now,
        };
        rma.StatusChangedOnUtc = rma.RequestedOnUtc;

        var failures = new List<ValidationFailure>();
        foreach (var input in inputs)
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == input.OrderLineItemId)
                ?? throw new NotFoundException(nameof(OrderLineItem), input.OrderLineItemId);
            var eligible = eligibility.Lines.First(l => l.OrderLineItemId == line.Id);

            if (!eligible.Eligible)
            {
                failures.Add(new ValidationFailure("Lines", $"'{line.ProductName}': {eligible.IneligibleReason ?? "cannot be returned"}."));
                continue;
            }
            if (input.Quantity > eligible.QuantityReturnable)
            {
                failures.Add(new ValidationFailure("Lines", $"'{line.ProductName}': at most {eligible.QuantityReturnable} can be returned."));
                continue;
            }

            RmaReason? reason = null;
            if (input.ReasonId is Guid rid)
            {
                if (!reasons.TryGetValue(rid, out reason) || (!forStaff && !(reason.IsActive && reason.CustomerVisible)))
                {
                    failures.Add(new ValidationFailure("Lines", $"'{line.ProductName}': choose a valid reason."));
                    continue;
                }
                if (!forStaff && reason.RequiresComment && string.IsNullOrWhiteSpace(input.Comment))
                {
                    failures.Add(new ValidationFailure("Lines", $"'{line.ProductName}': please describe the problem ({reason.Name})."));
                    continue;
                }
            }

            rma.Lines.Add(new RmaLineItem
            {
                RmaId = rma.Id,
                OrderLineItemId = line.Id,
                ProductId = line.ProductId,
                ProductVariantId = line.ProductVariantId,
                ProductName = line.ProductName,
                Sku = line.Sku,
                Quantity = input.Quantity,
                ReasonId = reason?.Id,
                LineReason = reason?.Name,
                CustomerComment = string.IsNullOrWhiteSpace(input.Comment) ? null : input.Comment.Trim(),
                ConditionReported = input.Condition,
                UnitRefund = eligible.UnitPaid,
                FeeApplies = reason?.FeeApplies ?? false,
                MerchantFault = reason?.MerchantFault ?? false,
            });
            if (eligible.RequiresFfl) rma.RequiresFfl = true;
            if (eligible.ShipRestricted) rma.ShipRestricted = true;
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
        return rma;
    }
}

/// <summary>Approval side effects shared by review, auto-approve and staff create.</summary>
internal static class RmaApproval
{
    public static void Approve(IApplicationDbContext db, Domain.Entities.Rma rma, ReturnSettingsDto settings, RmaActor actor, string? note, DateTime now)
    {
        RmaWorkflow.Transition(db, rma, RmaStatus.Approved, actor, note, now);
        rma.LabelExpiresAtUtc = now.AddDays(Math.Max(1, settings.LabelExpiryDays));
        rma.ExpiryWarningSentOnUtc = null;
        if (rma.ShipRestricted && string.IsNullOrWhiteSpace(rma.ReturnInstructions))
            rma.ReturnInstructions = "This item cannot be shipped back by a parcel carrier. Our team will contact you with return arrangements.";
    }

    public static Task NotifyApprovedAsync(IEmailTemplateSender templates, ReturnSettingsDto settings, Order order, Domain.Entities.Rma rma, CancellationToken ct)
        => RmaSupport.SendCustomerAsync(templates, "return.approved", order, rma, ApprovalVars(settings, rma), ct);

    public static Dictionary<string, string> ApprovalVars(ReturnSettingsDto settings, Domain.Entities.Rma rma) => new()
    {
        ["returnAddress"] = settings.ReturnAddress.ToHtml(),
        ["returnInstructions"] = RmaSupport.Encode(rma.ReturnInstructions),
        ["expiryDate"] = rma.LabelExpiresAtUtc?.ToString("MMM d, yyyy") ?? string.Empty,
        ["returnLabelBlock"] = string.IsNullOrWhiteSpace(rma.LabelUrl)
            ? string.Empty
            : $"<a href=\"{RmaSupport.Encode(rma.LabelUrl)}\">Download your return label</a>"
              + (string.IsNullOrWhiteSpace(rma.TrackingNumber) ? string.Empty : $" (tracking {RmaSupport.Encode(rma.TrackingNumber)})"),
    };
}
