using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Store-credit ledger (WO-48). Customer is a NoAction lookup (it is soft-deletable); balance = SUM(Amount).</summary>
public class StoreCreditTransactionConfiguration : IEntityTypeConfiguration<StoreCreditTransaction>
{
    public void Configure(EntityTypeBuilder<StoreCreditTransaction> b)
    {
        b.ToTable("StoreCreditTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(512);
        b.Property(x => x.Type).HasConversion<int>();

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.RmaId);
    }
}
