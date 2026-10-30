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
