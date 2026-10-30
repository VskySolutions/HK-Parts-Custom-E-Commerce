using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Setup;

public record GetSetupStatusQuery : IRequest<SetupStatusDto>;

public class GetSetupStatusQueryHandler : IRequestHandler<GetSetupStatusQuery, SetupStatusDto>
{
    private readonly IApplicationDbContext _db;
    private readonly ISettingsService _settings;

    public GetSetupStatusQueryHandler(IApplicationDbContext db, ISettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<SetupStatusDto> Handle(GetSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var superAdminName = nameof(RoleType.SuperAdmin);
        var superAdminExists = await _db.UserRoles
            .AnyAsync(ur => ur.Role!.Name == superAdminName, cancellationToken);
        var completed = await _settings.GetAsync<bool>("setup.completed", cancellationToken);

        return new SetupStatusDto
        {
            SetupCompleted = completed,
            SuperAdminExists = superAdminExists,
        };
    }
}
