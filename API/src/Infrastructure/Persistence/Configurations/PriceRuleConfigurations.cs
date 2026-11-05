using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class PriceRuleConfiguration : IEntityTypeConfiguration<PriceRule>
{
    public void Configure(EntityTypeBuilder<PriceRule> b)
    {
        b.ToTable("PriceRules", t =>
        {
            t.HasCheckConstraint("CK_PriceRules_DateRange",
                "[StartDate] IS NULL OR [EndDate] IS NULL OR [EndDate] >= [StartDate]");
            t.HasCheckConstraint("CK_PriceRules_DiscountAmount", "[DiscountAmount] > 0");
            t.HasCheckConstraint("CK_PriceRules_PercentMax", "[DiscountType] <> 1 OR [DiscountAmount] <= 100");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Priority).HasDefaultValue(0);
        b.Property(x => x.StopFurtherRules).HasDefaultValue(false);
        b.Property(x => x.DiscountType).HasConversion<byte>();
        b.Property(x => x.DiscountAmount).HasPrecision(18, 4);
        b.Property(x => x.RowVersion).IsRowVersion();

        // Lookup FK: a store is soft-deleted, so never cascade from it.
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.StoreId);
        b.HasIndex(x => x.Name);
    }
}

public class PriceRuleCustomerGroupConfiguration : IEntityTypeConfiguration<PriceRuleCustomerGroup>
{
    public void Configure(EntityTypeBuilder<PriceRuleCustomerGroup> b)
    {
        b.ToTable("PriceRuleCustomerGroups");
        b.HasKey(x => new { x.PriceRuleId, x.CustomerGroupId });
        b.HasOne(x => x.PriceRule).WithMany(r => r.CustomerGroups)
            .HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PriceRuleCategoryConfiguration : IEntityTypeConfiguration<PriceRuleCategory>
{
    public void Configure(EntityTypeBuilder<PriceRuleCategory> b)
    {
        b.ToTable("PriceRuleCategories");
        b.HasKey(x => new { x.PriceRuleId, x.CategoryId });
        b.HasOne(x => x.PriceRule).WithMany(r => r.Categories)
            .HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);
        // Categories are soft-deleted; the lookup FK never cascades.
        b.HasOne(x => x.Category).WithMany()
            .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.CategoryId);
    }
}

public class PriceRuleProductPriceConfiguration : IEntityTypeConfiguration<PriceRuleProductPrice>
{
    public void Configure(EntityTypeBuilder<PriceRuleProductPrice> b)
    {
        b.ToTable("PriceRuleProductPrices");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityColumn();
        b.Property(x => x.BasePrice).HasPrecision(18, 4);
        b.Property(x => x.RulePrice).HasPrecision(18, 4);

        b.HasOne(x => x.PriceRule).WithMany()
            .HasForeignKey(x => x.PriceRuleId).OnDelete(DeleteBehavior.Cascade);

        // One row per rule × product/variant × group. (SQL Server unique indexes treat NULL as a value, so
        // the product-level row with a NULL variant is unique too.)
        b.HasIndex(x => new { x.PriceRuleId, x.ProductId, x.ProductVariantId, x.CustomerGroupId }).IsUnique().HasFilter(null);

        // Covering index for price resolution — the resolver never touches the base table.
        b.HasIndex(x => new { x.ProductId, x.CustomerGroupId, x.StoreId })
            .IncludeProperties(x => new
            {
                x.ProductVariantId, x.PriceRuleId, x.RulePrice, x.StartDate, x.EndDate, x.Priority, x.StopFurtherRules,
            })
            .HasDatabaseName("IX_PriceRuleProductPrices_Lookup");
    }
}
