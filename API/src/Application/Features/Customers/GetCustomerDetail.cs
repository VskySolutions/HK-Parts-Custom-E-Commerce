using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Application.Features.CustomerAddresses;
using HKPartsEcommerce.Application.Features.CustomerGroups;
using HKPartsEcommerce.Application.Features.Orders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Customers;

/// <summary>One of a customer's orders, summarized for the detail view's order history (WO-117).</summary>
public class CustomerOrderSummaryDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public DateTime PlacedOnUtc { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// Full admin view of a single customer (WO-117): profile, activation state, assigned pricing group,
/// read-only tax-exemption status, saved address book and an order-history summary.
/// </summary>
public class CustomerDetailDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    /// <summary>Effective exemption state — only ever set by an approved request (WO-126).</summary>
    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionCertificate { get; set; }
    public string? VatId { get; set; }

    /// <summary>
    /// Read-only workflow status of the customer's latest tax-exemption request: NotSubmitted,
    /// PendingReview, Approved or Rejected. Review actions live in the tax-exemption-requests queue,
    /// not here (WO-117 out of scope / WO-126).
    /// </summary>
    public string TaxExemptionStatus { get; set; } = TaxExemptionStatusNames.NotSubmitted;
    public Guid? LatestTaxExemptionRequestId { get; set; }
    public DateTime? TaxExemptionSubmittedOnUtc { get; set; }

    public string? WhatsAppPhoneNumber { get; set; }
    public bool WhatsAppOptIn { get; set; }

    /// <summary>The single assigned pricing group, or null for base pricing (AC-CUS-003.2).</summary>
    public CustomerGroupBriefDto? CustomerGroup { get; set; }
    public List<AddressDto> Addresses { get; set; } = new();

    public int OrderCount { get; set; }
    /// <summary>Lifetime value: the total of orders whose payment was captured (net of fully-refunded ones).</summary>
    public decimal TotalSpent { get; set; }
    public List<CustomerOrderSummaryDto> RecentOrders { get; set; } = new();
}

/// <summary>The four states the admin UI renders for tax exemption; "NotSubmitted" has no request row.</summary>
public static class TaxExemptionStatusNames
{
    public const string NotSubmitted = "NotSubmitted";
}

/// <summary>Loads the full admin detail for one customer (WO-117).</summary>
public record GetCustomerDetailQuery(Guid Id) : IRequest<CustomerDetailDto>;

public class GetCustomerDetailQueryHandler : IRequestHandler<GetCustomerDetailQuery, CustomerDetailDto>
{
    private readonly IApplicationDbContext _db;

    public GetCustomerDetailQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CustomerDetailDto> Handle(GetCustomerDetailQuery request, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.CustomerGroup)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Customer), request.Id);

        var addresses = await _db.CustomerAddresses.AsNoTracking()
            .Include(m => m.Address)
            .Where(m => m.CustomerId == request.Id)
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.AddressType)
            .ToListAsync(cancellationToken);

        var orders = await _db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == request.Id)
            .ExcludeUnpaidRedirect()
            .ExcludeInquiries()
            .OrderByDescending(o => o.PlacedOnUtc)
            .ToListAsync(cancellationToken);

        // Latest request drives the read-only status chip (WO-126 owns the review actions).
        var latestRequest = await _db.TaxExemptionRequests.AsNoTracking()
            .Where(r => r.CustomerId == request.Id)
            .OrderByDescending(r => r.SubmittedOnUtc)
            .Select(r => new { r.Id, r.Status, r.SubmittedOnUtc })
            .FirstOrDefaultAsync(cancellationToken);

        return new CustomerDetailDto
        {
            Id = customer.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = customer.User?.Email ?? string.Empty,
            PhoneNumber = customer.PhoneNumber,
            EmailVerified = customer.User?.EmailVerified ?? false,
            IsActive = customer.User?.IsActive ?? false,
            CreatedOnUtc = customer.CreatedOnUtc,
            IsTaxExempt = customer.IsTaxExempt,
            TaxExemptionCertificate = customer.TaxExemptionCertificate,
            VatId = customer.VatId,
            TaxExemptionStatus = latestRequest is null
                ? TaxExemptionStatusNames.NotSubmitted
                : latestRequest.Status.ToString(),
            LatestTaxExemptionRequestId = latestRequest?.Id,
            TaxExemptionSubmittedOnUtc = latestRequest?.SubmittedOnUtc,
            WhatsAppPhoneNumber = customer.WhatsAppPhoneNumber,
            WhatsAppOptIn = customer.WhatsAppOptIn,
            CustomerGroup = customer.CustomerGroup is null ? null : CustomerGroupBriefDto.From(customer.CustomerGroup),
            Addresses = addresses.Select(AddressDto.From).ToList(),
            OrderCount = orders.Count,
            TotalSpent = orders
                .Where(o => o.PaymentStatus is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded)
                .Sum(o => o.TotalAmount),
            RecentOrders = orders.Take(10).Select(o => new CustomerOrderSummaryDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                Status = o.Status,
                PaymentStatus = o.PaymentStatus,
                PlacedOnUtc = o.PlacedOnUtc,
                CurrencyCode = o.CurrencyCode,
                TotalAmount = o.TotalAmount,
            }).ToList(),
        };
    }
}
