using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Loyalty points balance (WO-27). One row per customer (unique CustomerId). Customer is a NoAction
/// lookup because it is soft-deletable and a second cascade path to Customers would trip SQL Server's
/// multiple-cascade-paths rule.
/// </summary>
public class CustomerPointsBalanceConfiguration : IEntityTypeConfiguration<CustomerPointsBalance>
{
    public void Configure(EntityTypeBuilder<CustomerPointsBalance> b)
    {
        b.ToTable("CustomerPointsBalances");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        // One balance row per customer.
        b.HasIndex(x => x.CustomerId).IsUnique();
    }
}

/// <summary>
/// Loyalty points ledger (WO-27). Append-only; the balance = the customer's latest entry's BalanceAfter
/// (and the sum of their signed Points). Customer is a NoAction lookup (soft-deletable). Indexed by
/// (CustomerId, CreatedOnUtc) so a customer's statement reads chronologically without a scan.
/// </summary>
public class PointsTransactionConfiguration : IEntityTypeConfiguration<PointsTransaction>
{
    public void Configure(EntityTypeBuilder<PointsTransaction> b)
    {
        b.ToTable("PointsTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(300);
        b.Property(x => x.Type).HasConversion<int>();

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.CustomerId, x.CreatedOnUtc });
    }
}
