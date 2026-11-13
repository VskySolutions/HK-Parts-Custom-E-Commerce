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
        b.Property(x => x.AuthorizedAmount).HasPrecision(18, 2);
        b.Property(x => x.AuthCode).HasMaxLength(20);
        b.Property(x => x.AvsResultCode).HasMaxLength(10);
        b.Property(x => x.CvvResultCode).HasMaxLength(10);
        b.Property(x => x.NetworkTransactionId).HasMaxLength(100);
        b.Property(x => x.CardType).HasMaxLength(50);
        b.Property(x => x.CardLastFour).HasMaxLength(4);
        b.Property(x => x.GatewayResponseCode).HasMaxLength(10);
        b.Property(x => x.GatewayReasonCode).HasMaxLength(20);
        b.Property(x => x.GatewayResponseText).HasMaxLength(1000);
        b.Property(x => x.CustomerProfileId).HasMaxLength(50);
        b.Property(x => x.CustomerPaymentProfileId).HasMaxLength(50);

        b.HasOne(x => x.Order)
            .WithMany(o => o.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.Status);
    }
}

/// <summary>Per-operation gateway history of a payment (owned child: cascades from its single parent).</summary>
public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("PaymentTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.TransactionId).HasMaxLength(200);
        b.Property(x => x.RefTransactionId).HasMaxLength(200);
        b.Property(x => x.ResponseCode).HasMaxLength(10);
        b.Property(x => x.ReasonCode).HasMaxLength(20);
        b.Property(x => x.Message).HasMaxLength(1000);

        b.HasOne(x => x.PaymentRecord)
            .WithMany(p => p.Transactions)
            .HasForeignKey(x => x.PaymentRecordId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.PaymentRecordId);
    }
}
