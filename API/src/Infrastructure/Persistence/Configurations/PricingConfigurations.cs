using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> b)
    {
        b.ToTable("Discounts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Scope).HasConversion<int>();
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.Property(x => x.MinimumOrderValue).HasPrecision(18, 2);
        b.HasIndex(x => x.Scope);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CouponCodeConfiguration : IEntityTypeConfiguration<CouponCode>
{
    public void Configure(EntityTypeBuilder<CouponCode> b)
    {
        b.ToTable("CouponCodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.UsageType).HasConversion<int>();

        b.HasOne(x => x.Discount)
            .WithMany(d => d.CouponCodes)
            .HasForeignKey(x => x.DiscountId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.Code).IsUnique().HasFilter("[Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);
    }
}
