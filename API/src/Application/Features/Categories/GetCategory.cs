using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Categories;

public record GetCategoryQuery(Guid Id) : IRequest<CategoryDto>;

public class GetCategoryQueryHandler : IRequestHandler<GetCategoryQuery, CategoryDto>
{
    private readonly IApplicationDbContext _db;

    public GetCategoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CategoryDto> Handle(GetCategoryQuery request, CancellationToken cancellationToken)
    {
        return await CategoryQueries.GetDtoAsync(_db, request.Id, cancellationToken);
    }
}
