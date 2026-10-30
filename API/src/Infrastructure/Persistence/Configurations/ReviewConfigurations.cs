using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Customer product reviews (WO-14). Product and Customer are NoAction lookups (a product/customer
/// delete must not cascade its reviews, and NoAction avoids SQL Server multiple-cascade-path errors).</summary>
public class ProductReviewConfiguration : IEntityTypeConfiguration<ProductReview>
{
    public void Configure(EntityTypeBuilder<ProductReview> b)
    {
        b.ToTable("ProductReviews");
        b.HasKey(x => x.Id);
        b.Property(x => x.Title).HasMaxLength(200);
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ReviewerName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ProductId, x.Status });
        b.HasQueryFilter(x => !x.Deleted);
    }
}
