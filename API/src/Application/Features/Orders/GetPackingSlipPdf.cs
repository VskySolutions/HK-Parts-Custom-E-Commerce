using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>Renders an order packing slip PDF (WO-47) as a byte stream for download.</summary>
public record GetPackingSlipPdfQuery(Guid OrderId) : IRequest<byte[]>;

public class GetPackingSlipPdfQueryHandler : IRequestHandler<GetPackingSlipPdfQuery, byte[]>
{
    private readonly IOrderDocumentService _documents;

    public GetPackingSlipPdfQueryHandler(IOrderDocumentService documents) => _documents = documents;

    public Task<byte[]> Handle(GetPackingSlipPdfQuery request, CancellationToken cancellationToken)
        => _documents.GeneratePackingSlipAsync(request.OrderId, cancellationToken);
}
