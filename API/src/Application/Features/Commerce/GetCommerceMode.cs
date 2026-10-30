using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Commerce;

/// <summary>Admin read of the tenant's commerce-mode configuration (REQ-INQ-001).</summary>
public record GetCommerceModeQuery : IRequest<CommerceModeDto>;

public class GetCommerceModeQueryHandler : IRequestHandler<GetCommerceModeQuery, CommerceModeDto>
{
    private readonly ICommerceModeService _commerce;

    public GetCommerceModeQueryHandler(ICommerceModeService commerce) => _commerce = commerce;

    public async Task<CommerceModeDto> Handle(GetCommerceModeQuery request, CancellationToken cancellationToken)
        => CommerceModeDto.From(await _commerce.GetAsync(cancellationToken));
}
