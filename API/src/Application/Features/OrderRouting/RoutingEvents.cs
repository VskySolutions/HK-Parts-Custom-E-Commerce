using MediatR;
using HKPartsEcommerce.Application.Common.Models;

namespace HKPartsEcommerce.Application.Features.OrderRouting;

/// <summary>Raised when the routing engine assigns an order to a store (REQ-STR-003).</summary>
public record OrderRouted(RoutingRequest Request, Guid AssignedStoreId) : INotification;

/// <summary>Raised when no eligible store can fulfil the order; the order enters the unrouted state (AC-STR-003.5).</summary>
public record OrderUnroutable(RoutingRequest Request) : INotification;
