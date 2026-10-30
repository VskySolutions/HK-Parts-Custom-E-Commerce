using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class CustomerGroupConfiguration : IEntityTypeConfiguration<CustomerGroup>
{
    public void Configure(EntityTypeBuilder<CustomerGroup> b)
    {
        b.ToTable("CustomerGroups");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(512);
        b.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CustomerGroupPriceConfiguration : IEntityTypeConfiguration<CustomerGroupPrice>
{
    public void Configure(EntityTypeBuilder<CustomerGroupPrice> b)
    {
        b.ToTable("CustomerGroupPrices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.HasOne(x => x.CustomerGroup).WithMany(g => g.GroupPrices).HasForeignKey(x => x.CustomerGroupId).OnDelete(DeleteBehavior.Cascade);
        // Looked up by (group, product, variant) on every storefront/checkout read. Unique: exactly one price
        // per key. HasFilter(null) overrides EF's default "[ProductVariantId] IS NOT NULL" filter so the
        // product-level rows (null variant) are covered too — SQL Server treats NULL = NULL as equal in a
        // filterless unique index, giving exactly one product-level price per (group, product).
        b.HasIndex(x => new { x.CustomerGroupId, x.ProductId, x.ProductVariantId }).IsUnique().HasFilter(null);
    }
}
