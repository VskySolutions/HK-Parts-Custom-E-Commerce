using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.Recaptcha;

/// <summary>Returns the (singleton) admin reCAPTCHA configuration; defaults when never configured.</summary>
public record GetRecaptchaConfigQuery : IRequest<RecaptchaConfigDto>;

public class GetRecaptchaConfigQueryHandler : IRequestHandler<GetRecaptchaConfigQuery, RecaptchaConfigDto>
{
    private readonly IApplicationDbContext _db;

    public GetRecaptchaConfigQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<RecaptchaConfigDto> Handle(GetRecaptchaConfigQuery request, CancellationToken cancellationToken)
    {
        var config = await _db.RecaptchaConfigs.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
                     ?? new RecaptchaConfig();
        return RecaptchaConfigDto.From(config);
    }
}
