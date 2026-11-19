using FluentValidation;
using MediatR;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>Customer-side actions on their own returns (RT-26, RT-33 replies, RT-45).</summary>
public abstract class RmaCustomerHandlerBase
{
    protected readonly IApplicationDbContext Db;
    protected readonly ICurrentUserService Current;
    protected readonly ISettingsService Settings;
    protected readonly IEmailTemplateSender Templates;
    protected readonly IDateTimeProvider Clock;

    protected RmaCustomerHandlerBase(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
    {
        Db = db;
        Current = current;
        Settings = settings;
        Templates = templates;
        Clock = clock;
    }

    /// <summary>Loads one of the caller's returns (a foreign one is not found) and checks the action is offered.</summary>
    protected async Task<(Domain.Entities.Rma Rma, RmaActor Actor)> LoadOwnAsync(Guid id, string action, CancellationToken ct)
    {
        var customer = await RmaSupport.CurrentCustomerAsync(Db, Current, ct);
        var rma = await RmaSupport.LoadAsync(Db, id, ct);
        if (rma.CustomerId != customer.Id)
            throw new NotFoundException(nameof(Domain.Entities.Rma), id);
        var probe = new RmaDto { Status = rma.Status, ReturnMethod = rma.ReturnMethod };
        if (!RmaActions.ForCustomer(probe).Contains(action))
            throw new ConflictException($"That is not possible while the return is {RmaWorkflow.Label(rma.Status)}.");
        return (rma, RmaActor.Customer(customer.Id, Current));
    }

    protected async Task<RmaDto> CustomerDtoAsync(Domain.Entities.Rma rma, CancellationToken ct)
        => (await RmaSupport.ToDtoAsync(Db, Settings, rma, null, Clock.UtcNow, ct)).ForCustomer();
}

/// <summary>The customer withdraws a return before it ships (RT-26).</summary>
public record CancelMyRmaCommand(Guid RmaId) : IRequest<RmaDto>;

public class CancelMyRmaCommandHandler : RmaCustomerHandlerBase, IRequestHandler<CancelMyRmaCommand, RmaDto>
{
    public CancelMyRmaCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(CancelMyRmaCommand request, CancellationToken ct)
    {
        var (rma, actor) = await LoadOwnAsync(request.RmaId, RmaActions.Cancel, ct);
        RmaWorkflow.Transition(Db, rma, RmaStatus.Cancelled, actor, "Cancelled by the customer." + RmaLabels.VoidReminder(rma), Clock.UtcNow);
        await Db.SaveChangesAsync(ct);
        return await CustomerDtoAsync(rma, ct);
    }
}

/// <summary>The customer replies on the return thread; staff are alerted.</summary>
public record AddMyRmaMessageCommand(Guid RmaId, string Body) : IRequest<RmaDto>;

public class AddMyRmaMessageCommandValidator : AbstractValidator<AddMyRmaMessageCommand>
{
    public AddMyRmaMessageCommandValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
}

public class AddMyRmaMessageCommandHandler : RmaCustomerHandlerBase, IRequestHandler<AddMyRmaMessageCommand, RmaDto>
{
    public AddMyRmaMessageCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(AddMyRmaMessageCommand request, CancellationToken ct)
    {
        var (rma, actor) = await LoadOwnAsync(request.RmaId, RmaActions.Message, ct);
        RmaMessaging.Add(Db, rma, actor, request.Body, isInternal: false, Clock.UtcNow);
        await Db.SaveChangesAsync(ct);

        var order = await RmaSupport.LoadOrderAsync(Db, rma.OrderId, ct, tracked: false);
        var settings = await ReturnSettingsStore.LoadAsync(Settings, ct);
        await RmaSupport.SendStaffAsync(Templates, settings, order, rma, "Customer replied", request.Body.Trim(), ct);
        return await CustomerDtoAsync(rma, ct);
    }
}

/// <summary>A self-ship customer enters the carrier and tracking number; the return moves to In transit (RT-45).</summary>
public record SetMyRmaTrackingCommand(Guid RmaId, string Carrier, string TrackingNumber) : IRequest<RmaDto>;

public class SetMyRmaTrackingCommandValidator : AbstractValidator<SetMyRmaTrackingCommand>
{
    public SetMyRmaTrackingCommandValidator()
    {
        RuleFor(x => x.Carrier).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(100);
    }
}

public class SetMyRmaTrackingCommandHandler : RmaCustomerHandlerBase, IRequestHandler<SetMyRmaTrackingCommand, RmaDto>
{
    public SetMyRmaTrackingCommandHandler(IApplicationDbContext db, ICurrentUserService current, ISettingsService settings, IEmailTemplateSender templates, IDateTimeProvider clock)
        : base(db, current, settings, templates, clock) { }

    public async Task<RmaDto> Handle(SetMyRmaTrackingCommand request, CancellationToken ct)
    {
        var (rma, actor) = await LoadOwnAsync(request.RmaId, RmaActions.SetTracking, ct);
        rma.LabelCarrier = request.Carrier.Trim();
        rma.TrackingNumber = request.TrackingNumber.Trim();
        RmaWorkflow.Transition(Db, rma, RmaStatus.InTransit, actor, $"Customer shipped it: {rma.LabelCarrier} {rma.TrackingNumber}.", Clock.UtcNow);
        await Db.SaveChangesAsync(ct);
        return await CustomerDtoAsync(rma, ct);
    }
}
