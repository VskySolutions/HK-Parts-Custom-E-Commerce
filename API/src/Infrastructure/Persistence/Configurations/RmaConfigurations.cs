using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class RmaConfiguration : IEntityTypeConfiguration<Rma>
{
    public void Configure(EntityTypeBuilder<Rma> b)
    {
        b.ToTable("Rmas");
        b.HasKey(x => x.Id);
        b.Property(x => x.RmaNumber).HasMaxLength(64).IsRequired();
        b.Property(x => x.Reason).HasMaxLength(1024);
        b.Property(x => x.ResolutionNotes).HasMaxLength(1024);
        b.Property(x => x.RefundedAmount).HasPrecision(18, 2);
        b.Property(x => x.StoreCreditIssued).HasPrecision(18, 2);

        // Order is a lookup here; avoid a second cascade path (Order already cascades its own children).
        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.RmaNumber).IsUnique();
        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.Status);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class RmaLineItemConfiguration : IEntityTypeConfiguration<RmaLineItem>
{
    public void Configure(EntityTypeBuilder<RmaLineItem> b)
    {
        b.ToTable("RmaLineItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).HasMaxLength(400);
        b.Property(x => x.Sku).HasMaxLength(400);
        b.Property(x => x.LineReason).HasMaxLength(512);

        b.HasOne(x => x.Rma)
            .WithMany(r => r.Lines)
            .HasForeignKey(x => x.RmaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.RmaId);
    }
}
