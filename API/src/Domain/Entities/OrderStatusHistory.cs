using HKPartsEcommerce.Domain.Common;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Domain.Entities;

/// <summary>
/// An immutable audit entry appended on every order status transition (AC-ORD-001.3): who changed it,
/// when, and the from/to statuses.
/// </summary>
public class OrderStatusHistory : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }

    public OrderStatus FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }

    public Guid? ChangedById { get; set; }
    public DateTime ChangedOnUtc { get; set; }
    public string? Note { get; set; }
}
