using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Commerce;

/// <summary>
/// Public commerce config for the storefront (REQ-INQ-001). The /shop area loads this once and branches
/// the whole buying flow off it, so an inquiry-only tenant never renders a payment step.
/// </summary>
public record GetPublicCommerceConfigQuery : IRequest<PublicCommerceConfigDto>;

public class GetPublicCommerceConfigQueryHandler
    : IRequestHandler<GetPublicCommerceConfigQuery, PublicCommerceConfigDto>
{
    private readonly ICommerceModeService _commerce;

    public GetPublicCommerceConfigQueryHandler(ICommerceModeService commerce) => _commerce = commerce;

    public async Task<PublicCommerceConfigDto> Handle(
        GetPublicCommerceConfigQuery request, CancellationToken cancellationToken)
        => PublicCommerceConfigDto.From(await _commerce.GetAsync(cancellationToken));
}
