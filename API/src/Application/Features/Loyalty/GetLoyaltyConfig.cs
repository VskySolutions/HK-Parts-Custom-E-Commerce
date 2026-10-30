using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>Returns the loyalty program configuration (enabled flag + earn/redeem rates), defaults applied (WO-27).</summary>
public record GetLoyaltyConfigQuery : IRequest<LoyaltyConfigDto>;

public class GetLoyaltyConfigQueryHandler : IRequestHandler<GetLoyaltyConfigQuery, LoyaltyConfigDto>
{
    private readonly IRewardPointsService _points;

    public GetLoyaltyConfigQueryHandler(IRewardPointsService points) => _points = points;

    public async Task<LoyaltyConfigDto> Handle(GetLoyaltyConfigQuery request, CancellationToken cancellationToken)
        => LoyaltyConfigDto.From(await _points.GetConfigAsync(cancellationToken));
}
