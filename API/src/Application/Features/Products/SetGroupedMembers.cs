using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Products;

/// <summary>Replaces the member products of a grouped product with the requested set (AC-CAT-001.3).</summary>
public record SetGroupedMembersCommand(Guid ProductId, List<Guid> MemberProductIds) : IRequest<ProductDto>;

public class SetGroupedMembersCommandHandler : IRequestHandler<SetGroupedMembersCommand, ProductDto>
{
    private readonly IApplicationDbContext _db;

    public SetGroupedMembersCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ProductDto> Handle(SetGroupedMembersCommand request, CancellationToken cancellationToken)
    {
        var product = await _db.Products
            .AsSplitQuery()
            .WithFullGraph()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var memberIds = request.MemberProductIds?.Distinct().ToList() ?? new List<Guid>();
        if (memberIds.Contains(product.Id))
            throw new ConflictException("A grouped product cannot contain itself as a member.");

        if (memberIds.Count > 0)
        {
            var found = await _db.Products
                .Where(p => memberIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);
            var missing = memberIds.Except(found).ToList();
            if (missing.Count > 0)
                throw new NotFoundException(nameof(Product), missing[0]);
        }

        var desired = memberIds.ToHashSet();
        foreach (var row in product.GroupedMembers.Where(m => !desired.Contains(m.MemberProductId)).ToList())
            product.GroupedMembers.Remove(row);

        for (var i = 0; i < memberIds.Count; i++)
        {
            var memberId = memberIds[i];
            var row = product.GroupedMembers.FirstOrDefault(m => m.MemberProductId == memberId);
            if (row is null)
                product.GroupedMembers.Add(new GroupedProductMember { GroupedProductId = product.Id, MemberProductId = memberId, DisplayOrder = i });
            else
                row.DisplayOrder = i;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ProductDto.From(product);
    }
}
