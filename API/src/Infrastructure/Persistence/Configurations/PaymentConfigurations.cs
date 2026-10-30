using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class PaymentRecordConfiguration : IEntityTypeConfiguration<PaymentRecord>
{
    public void Configure(EntityTypeBuilder<PaymentRecord> b)
    {
        b.ToTable("PaymentRecords");
        b.HasKey(x => x.Id);
        b.Property(x => x.Method).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.GatewayName).HasMaxLength(100);
        b.Property(x => x.PaymentInstrument).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.AuthorizationId).HasMaxLength(200);
        b.Property(x => x.TransactionId).HasMaxLength(200);
        b.Property(x => x.GatewayReference).HasMaxLength(200);

        b.HasOne(x => x.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.Status);
    }
}
