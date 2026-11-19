using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>Who performed an RMA action; written to the history and message rows.</summary>
public record RmaActor(RmaActorType Type, Guid? Id, string? Name, string? IpAddress)
{
    public static readonly RmaActor System = new(RmaActorType.System, null, "System", null);

    public static RmaActor Admin(ICurrentUserService current)
        => new(RmaActorType.Admin, current.UserId, current.Email, current.IpAddress);

    public static RmaActor Customer(Guid? customerId, ICurrentUserService current)
        => new(RmaActorType.Customer, customerId, current.Email, current.IpAddress);
}

/// <summary>
/// The RMA state machine (order-return-requirements RT-01 / RT-02). Every status change goes through
/// <see cref="Transition"/>, which refuses moves the diagram does not allow and appends a history row.
/// </summary>
public static class RmaWorkflow
{
    private static readonly Dictionary<RmaStatus, RmaStatus[]> Allowed = new()
    {
        // Received straight from Requested/Approved = walk-in / counter return (RT-03, recorded as skipped steps).
        [RmaStatus.Requested] = new[] { RmaStatus.Approved, RmaStatus.Rejected, RmaStatus.Cancelled, RmaStatus.Received },
        [RmaStatus.Approved] = new[] { RmaStatus.InTransit, RmaStatus.Received, RmaStatus.Cancelled, RmaStatus.Expired, RmaStatus.Rejected },
        [RmaStatus.InTransit] = new[] { RmaStatus.Received },
        [RmaStatus.Received] = new[] { RmaStatus.Inspected, RmaStatus.Closed },
        [RmaStatus.Inspected] = new[] { RmaStatus.Refunded, RmaStatus.Exchanged, RmaStatus.Closed },
        [RmaStatus.Refunded] = new[] { RmaStatus.Closed },
        [RmaStatus.Exchanged] = new[] { RmaStatus.Closed },
        // Reopen (managers): back to Inspected so a remaining amount can be settled (RT-33).
        [RmaStatus.Closed] = new[] { RmaStatus.Inspected },
        [RmaStatus.Rejected] = Array.Empty<RmaStatus>(),
        [RmaStatus.Cancelled] = Array.Empty<RmaStatus>(),
        [RmaStatus.Expired] = Array.Empty<RmaStatus>(),
    };

    /// <summary>Statuses whose units are no longer claimed: they can be requested again on a new RMA.</summary>
    public static readonly RmaStatus[] Released = { RmaStatus.Rejected, RmaStatus.Cancelled, RmaStatus.Expired };

    /// <summary>Statuses after inspection, where the accepted (not the requested) quantity is what came back.</summary>
    public static readonly RmaStatus[] Settled = { RmaStatus.Inspected, RmaStatus.Refunded, RmaStatus.Exchanged, RmaStatus.Closed };

    /// <summary>Still being worked: counted in the admin badge and blocks nothing else.</summary>
    public static bool IsOpen(RmaStatus s) => s is not (RmaStatus.Rejected or RmaStatus.Cancelled or RmaStatus.Expired or RmaStatus.Closed);

    public static bool CanTransition(RmaStatus from, RmaStatus to)
        => Allowed.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Moves <paramref name="rma"/> to <paramref name="to"/> and records the change.</summary>
    public static void Transition(IApplicationDbContext db, Domain.Entities.Rma rma, RmaStatus to, RmaActor actor, string? note, DateTime now)
    {
        var from = rma.Status;
        if (!CanTransition(from, to))
            throw new ConflictException($"A return that is {Label(from)} cannot be moved to {Label(to)}.");

        rma.Status = to;
        rma.StatusChangedOnUtc = now;
        switch (to)
        {
            case RmaStatus.Approved: rma.ApprovedOnUtc ??= now; break;
            case RmaStatus.Received: rma.ReceivedOnUtc ??= now; break;
            case RmaStatus.Refunded: rma.RefundedOnUtc = now; break;
            case RmaStatus.Closed: rma.ClosedOnUtc = now; break;
            case RmaStatus.Inspected when from == RmaStatus.Closed: rma.ClosedOnUtc = null; break;
        }
        if (to is RmaStatus.Rejected or RmaStatus.Refunded or RmaStatus.Exchanged or RmaStatus.Closed)
        {
            rma.ResolvedOnUtc = now;
            if (actor.Type == RmaActorType.Admin)
                rma.ResolvedById = actor.Id;
        }

        AddHistory(db, rma, from, to, actor, note, now);
    }

    /// <summary>A history row that is not a status change (label added, refund failed, restock, …).</summary>
    public static void Note(IApplicationDbContext db, Domain.Entities.Rma rma, RmaActor actor, string note, DateTime now)
        => AddHistory(db, rma, rma.Status, rma.Status, actor, note, now);

    private static void AddHistory(IApplicationDbContext db, Domain.Entities.Rma rma, RmaStatus? from, RmaStatus to, RmaActor actor, string? note, DateTime now)
    {
        // Added through the DbSet (not the navigation): a preset Guid key found via a navigation would be
        // treated as an existing row and UPDATEd.
        db.RmaHistory.Add(new RmaHistory
        {
            RmaId = rma.Id,
            FromStatus = from,
            ToStatus = to,
            ActorType = actor.Type,
            ActorId = actor.Id,
            ActorName = Truncate(actor.Name, 256),
            Note = Truncate(note, 2000),
            IpAddress = actor.IpAddress,
            CreatedOnUtc = now,
        });
    }

    public static string Label(RmaStatus s) => s switch
    {
        RmaStatus.InTransit => "in transit",
        _ => s.ToString().ToLowerInvariant(),
    };

    internal static string? Truncate(string? s, int max)
        => s is null ? null : s.Length <= max ? s : s[..max];
}
