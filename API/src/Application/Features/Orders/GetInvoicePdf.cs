using MediatR;
using HKPartsEcommerce.Application.Common.Interfaces;

namespace HKPartsEcommerce.Application.Features.Orders;

/// <summary>Renders an order invoice PDF (WO-47) as a byte stream for download.</summary>
public record GetInvoicePdfQuery(Guid OrderId) : IRequest<byte[]>;

public class GetInvoicePdfQueryHandler : IRequestHandler<GetInvoicePdfQuery, byte[]>
{
    private readonly IOrderDocumentService _documents;

    public GetInvoicePdfQueryHandler(IOrderDocumentService documents) => _documents = documents;

    public Task<byte[]> Handle(GetInvoicePdfQuery request, CancellationToken cancellationToken)
        => _documents.GenerateInvoiceAsync(request.OrderId, cancellationToken);
}
