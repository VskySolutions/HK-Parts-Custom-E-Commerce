using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Rma;

/// <summary>A return reason (section 6.2 / 15): the flags drive fees, label deductions, auto-approve and form rules.</summary>
public class RmaReasonDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool CustomerVisible { get; set; }
    public bool FeeApplies { get; set; }
    public bool MerchantFault { get; set; }
    public bool AutoApprove { get; set; }
    public bool RequiresComment { get; set; }
    public bool RequiresPhoto { get; set; }
    public int SortOrder { get; set; }

    public static RmaReasonDto From(RmaReason r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        IsActive = r.IsActive,
        CustomerVisible = r.CustomerVisible,
        FeeApplies = r.FeeApplies,
        MerchantFault = r.MerchantFault,
        AutoApprove = r.AutoApprove,
        RequiresComment = r.RequiresComment,
        RequiresPhoto = r.RequiresPhoto,
        SortOrder = r.SortOrder,
    };
}

/// <summary>Lists return reasons. The storefront asks for active, customer-visible ones only.</summary>
public record ListRmaReasonsQuery(bool CustomerOnly = false) : IRequest<List<RmaReasonDto>>;

public class ListRmaReasonsQueryHandler : IRequestHandler<ListRmaReasonsQuery, List<RmaReasonDto>>
{
    private readonly IApplicationDbContext _db;
    public ListRmaReasonsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<List<RmaReasonDto>> Handle(ListRmaReasonsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.RmaReasons.AsNoTracking();
        if (request.CustomerOnly)
            query = query.Where(r => r.IsActive && r.CustomerVisible);
        var rows = await query.OrderBy(r => r.SortOrder).ThenBy(r => r.Name).ToListAsync(cancellationToken);
        return rows.Select(RmaReasonDto.From).ToList();
    }
}

/// <summary>Creates (Id null) or updates a return reason.</summary>
public record SaveRmaReasonCommand(
    Guid? Id,
    string Name,
    bool IsActive = true,
    bool CustomerVisible = true,
    bool FeeApplies = false,
    bool MerchantFault = false,
    bool AutoApprove = false,
    bool RequiresComment = false,
    bool RequiresPhoto = false,
    int SortOrder = 0) : IRequest<RmaReasonDto>;

public class SaveRmaReasonCommandValidator : AbstractValidator<SaveRmaReasonCommand>
{
    public SaveRmaReasonCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
    }
}

public class SaveRmaReasonCommandHandler : IRequestHandler<SaveRmaReasonCommand, RmaReasonDto>
{
    private readonly IApplicationDbContext _db;
    public SaveRmaReasonCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RmaReasonDto> Handle(SaveRmaReasonCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var duplicate = await _db.RmaReasons.AnyAsync(r => r.Name == name && r.Id != request.Id, cancellationToken);
        if (duplicate)
            throw new ConflictException($"A return reason named '{name}' already exists.");

        RmaReason reason;
        if (request.Id is Guid id)
        {
            reason = await _db.RmaReasons.FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
                ?? throw new NotFoundException(nameof(RmaReason), id);
        }
        else
        {
            reason = new RmaReason();
            _db.RmaReasons.Add(reason);
        }

        reason.Name = name;
        reason.IsActive = request.IsActive;
        reason.CustomerVisible = request.CustomerVisible;
        reason.FeeApplies = request.FeeApplies;
        reason.MerchantFault = request.MerchantFault;
        reason.AutoApprove = request.AutoApprove;
        reason.RequiresComment = request.RequiresComment;
        reason.RequiresPhoto = request.RequiresPhoto;
        reason.SortOrder = request.SortOrder;

        await _db.SaveChangesAsync(cancellationToken);
        return RmaReasonDto.From(reason);
    }
}

/// <summary>
/// Deletes a reason that no return uses; a reason already on a return is deactivated instead, so settled
/// returns keep their history (lines also snapshot the name).
/// </summary>
public record DeleteRmaReasonCommand(Guid Id) : IRequest;

public class DeleteRmaReasonCommandHandler : IRequestHandler<DeleteRmaReasonCommand>
{
    private readonly IApplicationDbContext _db;
    public DeleteRmaReasonCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteRmaReasonCommand request, CancellationToken cancellationToken)
    {
        var reason = await _db.RmaReasons.FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(RmaReason), request.Id);

        var used = await _db.RmaLineItems.AnyAsync(l => l.ReasonId == reason.Id, cancellationToken);
        if (used)
            reason.IsActive = false;
        else
            _db.RmaReasons.Remove(reason);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
