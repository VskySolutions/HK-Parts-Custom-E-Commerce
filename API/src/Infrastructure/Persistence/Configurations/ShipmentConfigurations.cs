using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> b)
    {
        b.ToTable("Shipments");
        b.HasKey(x => x.Id);
        b.Property(x => x.ShipmentNumber).HasMaxLength(64).IsRequired();
        b.Property(x => x.Carrier).HasMaxLength(128);
        b.Property(x => x.ServiceName).HasMaxLength(128);
        b.Property(x => x.TrackingNumber).HasMaxLength(128);
        b.Property(x => x.LabelAssetKey).HasMaxLength(512);
        b.Property(x => x.LabelPdfUrl).HasMaxLength(2048);
        b.Property(x => x.Notes).HasMaxLength(1024);

        // Order is the single parent (cascade the shipment when its order is removed).
        b.HasOne(x => x.Order)
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ShipmentNumber).IsUnique();
        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.TrackingNumber);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ShipmentLineItemConfiguration : IEntityTypeConfiguration<ShipmentLineItem>
{
    public void Configure(EntityTypeBuilder<ShipmentLineItem> b)
    {
        b.ToTable("ShipmentLineItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).HasMaxLength(400);
        b.Property(x => x.Sku).HasMaxLength(400);

        b.HasOne(x => x.Shipment)
            .WithMany(s => s.Lines)
            .HasForeignKey(x => x.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ShipmentId);
    }
}

public class ShipmentTrackingConfiguration : IEntityTypeConfiguration<ShipmentTracking>
{
    public void Configure(EntityTypeBuilder<ShipmentTracking> b)
    {
        b.ToTable("ShipmentTrackingEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.RawStatus).HasMaxLength(128);
        b.Property(x => x.Description).HasMaxLength(512);
        b.Property(x => x.Location).HasMaxLength(256);

        b.HasOne(x => x.Shipment)
            .WithMany(s => s.TrackingEvents)
            .HasForeignKey(x => x.ShipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.ShipmentId);
    }
}
