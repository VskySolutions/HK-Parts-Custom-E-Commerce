using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Extensions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Common.Models;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Webhooks;

// ---- Create ------------------------------------------------------------------

/// <summary>Registers a webhook endpoint and returns its generated signing secret once (AC-PLT-003.1).</summary>
public record CreateWebhookSubscriptionCommand(string Url, List<string> EventTypes, string? Description = null)
    : IRequest<WebhookSubscriptionDto>;

public class CreateWebhookSubscriptionCommandValidator : AbstractValidator<CreateWebhookSubscriptionCommand>
{
    public CreateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid absolute http(s) URL is required.");
        RuleFor(x => x.EventTypes).NotEmpty();
        RuleForEach(x => x.EventTypes).NotEmpty().MaximumLength(128);
    }
}

public class CreateWebhookSubscriptionCommandHandler : IRequestHandler<CreateWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    public CreateWebhookSubscriptionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<WebhookSubscriptionDto> Handle(CreateWebhookSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = new WebhookSubscription
        {
            Url = request.Url,
            Secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            IsActive = true,
            Description = request.Description,
        };

        foreach (var eventType in request.EventTypes.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct())
            subscription.Events.Add(new WebhookSubscriptionEvent { EventType = eventType });

        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(cancellationToken);

        // The secret is shown exactly once, on creation.
        return WebhookSubscriptionDto.From(subscription, includeSecret: true);
    }
}

// ---- Update ------------------------------------------------------------------

/// <summary>
/// Updates a webhook endpoint's URL, subscribed events, description and active state (AC-PLT-003.1).
/// The signing secret is preserved (never rotated here) so existing receivers keep verifying.
/// </summary>
public record UpdateWebhookSubscriptionCommand(
    Guid Id, string Url, List<string> EventTypes, bool IsActive, string? Description = null)
    : IRequest<WebhookSubscriptionDto>;

public class UpdateWebhookSubscriptionCommandValidator : AbstractValidator<UpdateWebhookSubscriptionCommand>
{
    public UpdateWebhookSubscriptionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2048)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("A valid absolute http(s) URL is required.");
        RuleFor(x => x.EventTypes).NotEmpty();
        RuleForEach(x => x.EventTypes).NotEmpty().MaximumLength(128);
    }
}

public class UpdateWebhookSubscriptionCommandHandler : IRequestHandler<UpdateWebhookSubscriptionCommand, WebhookSubscriptionDto>
{
    private readonly IApplicationDbContext _db;
    public UpdateWebhookSubscriptionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<WebhookSubscriptionDto> Handle(UpdateWebhookSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _db.WebhookSubscriptions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(WebhookSubscription), request.Id);

        subscription.Url = request.Url;
        subscription.Description = request.Description;
        subscription.IsActive = request.IsActive;

        var desired = request.EventTypes.Select(e => e.Trim()).Where(e => e.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        // Reconcile the event set: drop the ones no longer wanted, add the new ones, keep the rest.
        var toRemove = subscription.Events.Where(e => !desired.Contains(e.EventType)).ToList();
        foreach (var e in toRemove)
        {
            subscription.Events.Remove(e);
            _db.WebhookSubscriptionEvents.Remove(e);
        }

        var existingTypes = subscription.Events.Select(e => e.EventType).ToHashSet(StringComparer.Ordinal);
        foreach (var eventType in desired.Where(e => !existingTypes.Contains(e)))
            subscription.Events.Add(new WebhookSubscriptionEvent { EventType = eventType });

        await _db.SaveChangesAsync(cancellationToken);
        return WebhookSubscriptionDto.From(subscription);
    }
}

// ---- List --------------------------------------------------------------------

/// <summary>Lists registered webhook endpoints (secrets are never returned) (AC-PLT-003.4).</summary>
public record ListWebhookSubscriptionsQuery : IRequest<List<WebhookSubscriptionDto>>;

public class ListWebhookSubscriptionsQueryHandler : IRequestHandler<ListWebhookSubscriptionsQuery, List<WebhookSubscriptionDto>>
{
    private readonly IApplicationDbContext _db;
    public ListWebhookSubscriptionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<WebhookSubscriptionDto>> Handle(ListWebhookSubscriptionsQuery request, CancellationToken cancellationToken)
    {
        var subs = await _db.WebhookSubscriptions
            .AsNoTracking()
            .Include(s => s.Events)
            .OrderByDescending(s => s.CreatedOnUtc)
            .ToListAsync(cancellationToken);
        return subs.Select(s => WebhookSubscriptionDto.From(s)).ToList();
    }
}

// ---- Delete ------------------------------------------------------------------

/// <summary>Removes a webhook endpoint (soft delete; idempotent).</summary>
public record DeleteWebhookSubscriptionCommand(Guid Id) : IRequest;

public class DeleteWebhookSubscriptionCommandHandler : IRequestHandler<DeleteWebhookSubscriptionCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteWebhookSubscriptionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteWebhookSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _db.WebhookSubscriptions.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (subscription is null)
            return;

        _db.WebhookSubscriptions.Remove(subscription);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

// ---- Deliveries --------------------------------------------------------------

/// <summary>Lists delivery history with response status + attempt count (AC-PLT-003.4).</summary>
public record ListWebhookDeliveriesQuery(Guid? SubscriptionId = null, int Page = 1, int PageSize = 50, string? SortBy = null, bool SortDescending = false)
    : IRequest<PaginatedList<WebhookDeliveryDto>>;

public class ListWebhookDeliveriesQueryHandler : IRequestHandler<ListWebhookDeliveriesQuery, PaginatedList<WebhookDeliveryDto>>
{
    // Sortable field -> entity property path. Anything else falls back to CreatedOnUtc desc.
    private static readonly IReadOnlyDictionary<string, string> SortMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["eventType"] = "EventType",
        ["status"] = "Status",
        ["attemptCount"] = "AttemptCount",
        ["lastResponseStatus"] = "LastResponseStatus",
        ["occurredAtUtc"] = "OccurredAtUtc",
        ["lastAttemptOnUtc"] = "LastAttemptOnUtc",
        ["nextAttemptOnUtc"] = "NextAttemptOnUtc",
    };

    private readonly IApplicationDbContext _db;
    public ListWebhookDeliveriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<PaginatedList<WebhookDeliveryDto>> Handle(ListWebhookDeliveriesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<WebhookDelivery> query = _db.WebhookDeliveries.AsNoTracking();
        if (request.SubscriptionId is Guid sid)
            query = query.Where(d => d.SubscriptionId == sid);

        var page = await PaginatedList<WebhookDelivery>.CreateAsync(
            query.ApplySort(request.SortBy, request.SortDescending, SortMap), request.Page, request.PageSize, cancellationToken);
        return new PaginatedList<WebhookDeliveryDto>(page.Items.Select(WebhookDeliveryDto.From).ToList(), page.TotalCount, page.PageNumber, page.PageSize);
    }
}
