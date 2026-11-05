using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.PriceRules;

// ---- Create ---------------------------------------------------------------------------------------------

/// <summary>Create a price rule, then queue a rebuild of its flat prices.</summary>
public record CreatePriceRuleCommand(
    Guid? StoreId,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    bool StopFurtherRules,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PriceRuleDiscountType DiscountType,
    decimal DiscountAmount,
    List<Guid>? CustomerGroupIds,
    List<Guid>? CategoryIds) : IRequest<PriceRuleDto>, IPriceRuleInput;

public class CreatePriceRuleValidator : PriceRuleInputValidator<CreatePriceRuleCommand>
{
    public CreatePriceRuleValidator(IApplicationDbContext db) : base(db) { }
}

public class CreatePriceRuleHandler : IRequestHandler<CreatePriceRuleCommand, PriceRuleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPriceRuleRebuildQueue _queue;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public CreatePriceRuleHandler(
        IApplicationDbContext db, IPriceRuleRebuildQueue queue, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _queue = queue;
        _user = user;
        _clock = clock;
    }

    public async Task<PriceRuleDto> Handle(CreatePriceRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = new PriceRule();
        PriceRuleWriter.Apply(rule, request);
        _db.PriceRules.Add(rule);

        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        var after = PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
        _db.AuditTrails.Add(PriceRuleWriter.Audit("PriceRule.Created", rule.Id, null, after.ToAuditSnapshot(), _user, _clock));

        await _db.SaveChangesAsync(cancellationToken);
        _queue.EnqueueRule(rule.Id);

        return PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
    }
}

// ---- Update ---------------------------------------------------------------------------------------------

/// <summary>Update a price rule (optimistic concurrency on <see cref="RowVersion"/>), then queue a rebuild.</summary>
public record UpdatePriceRuleCommand(
    Guid Id,
    Guid? StoreId,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    bool StopFurtherRules,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PriceRuleDiscountType DiscountType,
    decimal DiscountAmount,
    List<Guid>? CustomerGroupIds,
    List<Guid>? CategoryIds,
    string? RowVersion) : IRequest<PriceRuleDto>, IPriceRuleInput;

public class UpdatePriceRuleValidator : PriceRuleInputValidator<UpdatePriceRuleCommand>
{
    public UpdatePriceRuleValidator(IApplicationDbContext db) : base(db)
    {
        RuleFor(x => x.RowVersion)
            .NotEmpty().WithMessage("rowVersion is required.")
            .Must(BeBase64).WithMessage("rowVersion is not valid.");
    }

    private static bool BeBase64(string? value)
    {
        if (string.IsNullOrEmpty(value)) return true; // NotEmpty reports it
        var buffer = new Span<byte>(new byte[value.Length]);
        return Convert.TryFromBase64String(value, buffer, out _);
    }
}

public class UpdatePriceRuleHandler : IRequestHandler<UpdatePriceRuleCommand, PriceRuleDto>
{
    private const string ConflictMessage = "This rule was changed by someone else. Reload?";

    private readonly IApplicationDbContext _db;
    private readonly IPriceRuleRebuildQueue _queue;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public UpdatePriceRuleHandler(
        IApplicationDbContext db, IPriceRuleRebuildQueue queue, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _queue = queue;
        _user = user;
        _clock = clock;
    }

    public async Task<PriceRuleDto> Handle(UpdatePriceRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _db.PriceRules
            .Include(r => r.CustomerGroups)
            .Include(r => r.Categories)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceRule), request.Id);

        if (!rule.RowVersion.AsSpan().SequenceEqual(Convert.FromBase64String(request.RowVersion!)))
            throw new ConflictException(ConflictMessage);

        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        var before = PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow)).ToAuditSnapshot();

        PriceRuleWriter.Apply(rule, request);
        // Always touch the row so its rowversion moves even when only the group/category sets changed.
        rule.UpdatedOnUtc = _clock.UtcNow;

        var after = PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
        _db.AuditTrails.Add(PriceRuleWriter.Audit("PriceRule.Updated", rule.Id, before, after.ToAuditSnapshot(), _user, _clock));

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone saved between our read and our write.
            throw new ConflictException(ConflictMessage);
        }

        _queue.EnqueueRule(rule.Id);
        return PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
    }
}

// ---- Delete ---------------------------------------------------------------------------------------------

/// <summary>
/// Hard-deletes a rule. Its flat price rows go with it in the same statement (FK cascade), so storefront
/// prices revert immediately — no rebuild needed.
/// </summary>
public record DeletePriceRuleCommand(Guid Id) : IRequest;

public class DeletePriceRuleHandler : IRequestHandler<DeletePriceRuleCommand>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IDateTimeProvider _clock;

    public DeletePriceRuleHandler(IApplicationDbContext db, ICurrentUserService user, IDateTimeProvider clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public async Task Handle(DeletePriceRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await _db.PriceRules
            .Include(r => r.CustomerGroups)
            .Include(r => r.Categories)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PriceRule), request.Id);

        var before = PriceRuleDto.From(rule, DateOnly.FromDateTime(_clock.UtcNow)).ToAuditSnapshot();
        _db.PriceRules.Remove(rule);
        _db.AuditTrails.Add(PriceRuleWriter.Audit("PriceRule.Deleted", rule.Id, before, null, _user, _clock));
        await _db.SaveChangesAsync(cancellationToken);
    }
}

// ---- Apply ----------------------------------------------------------------------------------------------

/// <summary>Rebuild one rule's flat prices now (synchronously) and return the refreshed rule.</summary>
public record ApplyPriceRuleCommand(Guid Id) : IRequest<PriceRuleDto>;

public class ApplyPriceRuleHandler : IRequestHandler<ApplyPriceRuleCommand, PriceRuleDto>
{
    private readonly IApplicationDbContext _db;
    private readonly IPriceRuleEngine _engine;
    private readonly IDateTimeProvider _clock;

    public ApplyPriceRuleHandler(IApplicationDbContext db, IPriceRuleEngine engine, IDateTimeProvider clock)
    {
        _db = db;
        _engine = engine;
        _clock = clock;
    }

    public async Task<PriceRuleDto> Handle(ApplyPriceRuleCommand request, CancellationToken cancellationToken)
    {
        if (!await _db.PriceRules.AnyAsync(r => r.Id == request.Id, cancellationToken))
            throw new NotFoundException(nameof(PriceRule), request.Id);

        await _engine.RebuildAsync(request.Id, cancellationToken);

        var rule = await _db.PriceRules.AsNoTracking()
            .Include(r => r.CustomerGroups)
            .Include(r => r.Categories)
            .AsSplitQuery()
            .FirstAsync(r => r.Id == request.Id, cancellationToken);
        var clock = await PriceRuleClock.LoadAsync(_db, cancellationToken);
        return PriceRuleDto.From(rule, clock.TodayFor(rule.StoreId, _clock.UtcNow));
    }
}

/// <summary>Queue a rebuild of every rule (optionally one store's). Returns how many rules were queued.</summary>
public record ApplyAllPriceRulesCommand(Guid? StoreId) : IRequest<int>;

public class ApplyAllPriceRulesHandler : IRequestHandler<ApplyAllPriceRulesCommand, int>
{
    private readonly IApplicationDbContext _db;
    private readonly IPriceRuleRebuildQueue _queue;

    public ApplyAllPriceRulesHandler(IApplicationDbContext db, IPriceRuleRebuildQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<int> Handle(ApplyAllPriceRulesCommand request, CancellationToken cancellationToken)
    {
        var ids = await _db.PriceRules.AsNoTracking()
            .Where(r => request.StoreId == null || r.StoreId == request.StoreId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in ids)
            _queue.EnqueueRule(id);
        return ids.Count;
    }
}

// ---- Preview --------------------------------------------------------------------------------------------

/// <summary>What an unsaved rule would price: affected product count and the first 50 rows.</summary>
public record PreviewPriceRuleCommand(
    Guid? StoreId,
    string Name,
    string? Description,
    bool IsActive,
    int Priority,
    bool StopFurtherRules,
    DateOnly? StartDate,
    DateOnly? EndDate,
    PriceRuleDiscountType DiscountType,
    decimal DiscountAmount,
    List<Guid>? CustomerGroupIds,
    List<Guid>? CategoryIds) : IRequest<PriceRulePreviewResult>, IPriceRuleInput;

public class PreviewPriceRuleValidator : PriceRuleInputValidator<PreviewPriceRuleCommand>
{
    // The name is irrelevant to the prices, so an unnamed draft can still be previewed.
    public PreviewPriceRuleValidator(IApplicationDbContext db) : base(db, requireName: false) { }
}

public class PreviewPriceRuleHandler : IRequestHandler<PreviewPriceRuleCommand, PriceRulePreviewResult>
{
    private readonly IPriceRuleEngine _engine;

    public PreviewPriceRuleHandler(IPriceRuleEngine engine) => _engine = engine;

    public Task<PriceRulePreviewResult> Handle(PreviewPriceRuleCommand request, CancellationToken cancellationToken) =>
        // Preview ignores status and dates on purpose: it answers "what would this rule price?".
        _engine.PreviewAsync(new PriceRuleDraft(
            request.StoreId, IsActive: true, EndDate: null, request.DiscountType, request.DiscountAmount,
            request.CustomerGroupIds ?? new List<Guid>(), request.CategoryIds ?? new List<Guid>()), cancellationToken);
}
