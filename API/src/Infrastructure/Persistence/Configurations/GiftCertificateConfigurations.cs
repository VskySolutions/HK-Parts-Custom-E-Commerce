using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class GiftCertificateConfiguration : IEntityTypeConfiguration<GiftCertificate>
{
    public void Configure(EntityTypeBuilder<GiftCertificate> b)
    {
        b.ToTable("GiftCertificates", t =>
        {
            t.HasCheckConstraint("CK_GiftCertificates_CurrentBalance", "[CurrentBalance] >= 0");
            t.HasCheckConstraint("CK_GiftCertificates_OriginalBalance", "[OriginalBalance] >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.OriginalBalance).HasPrecision(18, 2);
        b.Property(x => x.CurrentBalance).HasPrecision(18, 2);
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Source).HasConversion<byte>();
        b.Property(x => x.SenderName).HasMaxLength(75);
        b.Property(x => x.SenderEmail).HasMaxLength(75);
        b.Property(x => x.RecipientName).HasMaxLength(75);
        b.Property(x => x.RecipientEmail).HasMaxLength(75);
        b.Property(x => x.Message).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();

        // Codes are stored upper-case, so a plain unique index is case-insensitive in effect. Deleted
        // certificates keep their code reserved (never re-issued).
        b.HasIndex(x => new { x.StoreId, x.Code }).IsUnique();
        // One certificate per purchased unit: re-running issuance can never duplicate.
        b.HasIndex(x => new { x.SourceOrderItemId, x.SourceUnitIndex }).IsUnique()
            .HasFilter("[SourceOrderItemId] IS NOT NULL");
        b.HasIndex(x => x.SourceOrderId);
        b.HasIndex(x => x.CreatedOnUtc);

        b.HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.SourceOrder).WithMany().HasForeignKey(x => x.SourceOrderId).OnDelete(DeleteBehavior.NoAction);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class GiftCertificateTransactionConfiguration : IEntityTypeConfiguration<GiftCertificateTransaction>
{
    public void Configure(EntityTypeBuilder<GiftCertificateTransaction> b)
    {
        b.ToTable("GiftCertificateTransactions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<byte>();
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        b.Property(x => x.Comment).HasMaxLength(255);
        b.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        b.HasOne(x => x.GiftCertificate).WithMany(g => g.Transactions)
            .HasForeignKey(x => x.GiftCertificateId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => new { x.GiftCertificateId, x.CreatedOnUtc });
        b.HasIndex(x => x.OrderId);
    }
}
