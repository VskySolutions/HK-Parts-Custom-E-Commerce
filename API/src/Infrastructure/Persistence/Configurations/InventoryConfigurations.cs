using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class InventoryLevelConfiguration : IEntityTypeConfiguration<InventoryLevel>
{
    public void Configure(EntityTypeBuilder<InventoryLevel> b)
    {
        b.ToTable("InventoryLevels");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Product)
            .WithMany(p => p.InventoryLevels)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ProductVariant)
            .WithMany(v => v.InventoryLevels)
            .HasForeignKey(x => x.ProductVariantId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.NoAction);

        // One inventory row per (product, variant, store). Filtered so multiple NULL variant rows
        // (simple products) remain unique per store.
        b.HasIndex(x => new { x.ProductId, x.ProductVariantId, x.StoreId }).IsUnique();
        b.HasIndex(x => x.StoreId);
    }
}

public class InventoryAuditLogConfiguration : IEntityTypeConfiguration<InventoryAuditLog>
{
    public void Configure(EntityTypeBuilder<InventoryAuditLog> b)
    {
        // Append-only history: the scanner validates these too, the constraint guards hand-written SQL.
        b.ToTable("InventoryAuditLogs", t => t.HasCheckConstraint("CK_InventoryAuditLogs_NonNegative",
            "([NewQty] IS NULL OR [NewQty] >= 0) AND ([NewPrice] IS NULL OR [NewPrice] >= 0)"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<int>();
        b.Property(x => x.OldPrice).HasPrecision(18, 2);
        b.Property(x => x.NewPrice).HasPrecision(18, 2);
        b.Property(x => x.ScannedCode).HasMaxLength(400);
        b.Property(x => x.Notes).HasMaxLength(500);
        b.Property(x => x.Source).HasConversion<int>();
        b.Property(x => x.MatchedBy).HasConversion<int?>();
        b.Property(x => x.ClientIp).HasMaxLength(45);
        b.Property(x => x.UserAgent).HasMaxLength(300);

        // Products / variants / stores are soft-deleted, so plain no-action lookups keep the history rows
        // intact and never cascade a log away.
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.CreatedOnUtc);
        b.HasIndex(x => new { x.ProductId, x.CreatedOnUtc });
        b.HasIndex(x => x.ProductVariantId);
        b.HasIndex(x => x.StoreId);
    }
}
