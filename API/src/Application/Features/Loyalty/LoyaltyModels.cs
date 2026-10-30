using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.Loyalty;

/// <summary>The loyalty program configuration: on/off plus the earn and redeem rates (WO-27).</summary>
public class LoyaltyConfigDto
{
    /// <summary>Whether the loyalty points program is active.</summary>
    public bool Enabled { get; set; }

    /// <summary>Points earned per 1 currency unit spent.</summary>
    public decimal EarnRate { get; set; }

    /// <summary>Points required per 1 currency unit of checkout discount.</summary>
    public decimal RedeemRate { get; set; }

    public static LoyaltyConfigDto From(LoyaltyConfig c) => new()
    {
        Enabled = c.Enabled,
        EarnRate = c.EarnRate,
        RedeemRate = c.RedeemRate,
    };
}

/// <summary>One entry in a customer's loyalty-points ledger (WO-27).</summary>
public class PointsTransactionDto
{
    public Guid Id { get; set; }
    public int Points { get; set; }
    public PointsTransactionType Type { get; set; }
    public string? Reason { get; set; }
    public Guid? OrderId { get; set; }
    public int BalanceAfter { get; set; }
    public DateTime CreatedOnUtc { get; set; }

    public static PointsTransactionDto From(PointsTransaction t) => new()
    {
        Id = t.Id,
        Points = t.Points,
        Type = t.Type,
        Reason = t.Reason,
        OrderId = t.OrderId,
        BalanceAfter = t.BalanceAfter,
        CreatedOnUtc = t.CreatedOnUtc,
    };
}

/// <summary>A customer's loyalty-points balance and recent ledger entries (WO-27).</summary>
public class PointsBalanceDto
{
    public Guid CustomerId { get; set; }
    public int Balance { get; set; }
    public List<PointsTransactionDto> Transactions { get; set; } = new();
}
