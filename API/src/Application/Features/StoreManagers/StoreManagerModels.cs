using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Application.Features.StoreManagers;

/// <summary>Links a store manager (user) to the single store they manage.</summary>
public class StoreManagerAssignmentDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public string? UserEmail { get; set; }
    public string? StoreName { get; set; }

    public static StoreManagerAssignmentDto From(StoreManagerAssignment a) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        StoreId = a.StoreId,
        UserEmail = a.User?.Email,
        StoreName = a.Store?.Name,
    };
}
