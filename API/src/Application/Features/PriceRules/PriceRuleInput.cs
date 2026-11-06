using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.PriceRules;

/// <summary>The editable shape shared by create, update and preview.</summary>
public interface IPriceRuleInput
{
    Guid? StoreId { get; }
    string Name { get; }
    string? Description { get; }
    bool IsActive { get; }
    int Priority { get; }
    bool StopFurtherRules { get; }
    DateOnly? StartDate { get; }
    DateOnly? EndDate { get; }
    PriceRuleDiscountType DiscountType { get; }
    decimal DiscountAmount { get; }
    List<Guid>? CustomerGroupIds { get; }
    List<Guid>? CategoryIds { get; }
}

/// <summary>
/// The rules the admin form enforces, plus existence checks: every group must exist (or be the Retail
/// sentinel) and every category must exist. Categories are global in this catalog — not per store — so
/// "exists in the current store" reduces to "exists".
/// </summary>
public abstract class PriceRuleInputValidator<T> : AbstractValidator<T> where T : IPriceRuleInput
{
    protected PriceRuleInputValidator(IApplicationDbContext db, bool requireName = true)
    {
        if (requireName)
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.").MaximumLength(100);

        RuleFor(x => x.Priority).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountType).IsInEnum();
        RuleFor(x => x.DiscountAmount).GreaterThan(0).WithMessage("Discount amount must be greater than 0.");
        RuleFor(x => x.DiscountAmount).LessThanOrEqualTo(100)
            .When(x => x.DiscountType == PriceRuleDiscountType.Percent)
            .WithMessage("A percent discount cannot exceed 100.");
        RuleFor(x => x.EndDate)
            .Must((x, end) => end is null || x.StartDate is null || end.Value >= x.StartDate.Value)
            .WithMessage("End date must be on or after the start date.");

        RuleFor(x => x.CustomerGroupIds)
            .NotNull().Must(ids => ids!.Count > 0).WithMessage("Select at least one customer group.")
            .MustAsync(async (ids, ct) =>
            {
                var real = ids!.Where(id => id != PriceRuleCustomerGroup.RetailGroupId).Distinct().ToList();
                if (real.Count == 0) return true;
                var found = await db.CustomerGroups.AsNoTracking().CountAsync(g => real.Contains(g.Id), ct);
                return found == real.Count;
            })
            .When(x => x.CustomerGroupIds is { Count: > 0 }, ApplyConditionTo.CurrentValidator)
            .WithMessage("One or more customer groups do not exist.");

        RuleFor(x => x.CategoryIds)
            .NotNull().Must(ids => ids!.Count > 0).WithMessage("Select at least one category.")
            .MustAsync(async (ids, ct) =>
            {
                var distinct = ids!.Distinct().ToList();
                var found = await db.Categories.AsNoTracking().CountAsync(c => distinct.Contains(c.Id), ct);
                return found == distinct.Count;
            })
            .When(x => x.CategoryIds is { Count: > 0 }, ApplyConditionTo.CurrentValidator)
            .WithMessage("One or more categories do not exist.");

        RuleFor(x => x.StoreId)
            .MustAsync(async (id, ct) => await db.Stores.AsNoTracking().AnyAsync(s => s.Id == id!.Value, ct))
            .When(x => x.StoreId.HasValue)
            .WithMessage("The store does not exist.");
    }
}

internal static class PriceRuleWriter
{
    /// <summary>Copies the input onto the entity and replaces its group/category sets.</summary>
    public static void Apply(PriceRule rule, IPriceRuleInput input)
    {
        rule.StoreId = input.StoreId;
        rule.Name = input.Name.Trim();
        rule.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description;
        rule.IsActive = input.IsActive;
        rule.Priority = input.Priority;
        rule.StopFurtherRules = input.StopFurtherRules;
        rule.StartDate = input.StartDate;
        rule.EndDate = input.EndDate;
        rule.DiscountType = input.DiscountType;
        rule.DiscountAmount = input.DiscountAmount;

        var groups = (input.CustomerGroupIds ?? new List<Guid>()).Distinct().ToHashSet();
        foreach (var stale in rule.CustomerGroups.Where(g => !groups.Contains(g.CustomerGroupId)).ToList())
            rule.CustomerGroups.Remove(stale);
        foreach (var id in groups.Where(id => rule.CustomerGroups.All(g => g.CustomerGroupId != id)))
            rule.CustomerGroups.Add(new PriceRuleCustomerGroup { PriceRuleId = rule.Id, CustomerGroupId = id });

        var categories = (input.CategoryIds ?? new List<Guid>()).Distinct().ToHashSet();
        foreach (var stale in rule.Categories.Where(c => !categories.Contains(c.CategoryId)).ToList())
            rule.Categories.Remove(stale);
        foreach (var id in categories.Where(id => rule.Categories.All(c => c.CategoryId != id)))
            rule.Categories.Add(new PriceRuleCategory { PriceRuleId = rule.Id, CategoryId = id });
    }

    /// <summary>An explicit before/after audit row (the generic interceptor only records changed column names).</summary>
    public static Domain.Entities.AuditTrail Audit(
        string action, Guid ruleId, object? before, object? after, ICurrentUserService user, IDateTimeProvider clock) => new()
    {
        UserId = user.UserId,
        ActorName = user.Email,
        Action = action,
        EntityType = nameof(PriceRule),
        EntityId = ruleId.ToString(),
        MetadataJson = JsonSerializer.Serialize(new { before, after }),
        TimestampUtc = clock.UtcNow,
    };
}
