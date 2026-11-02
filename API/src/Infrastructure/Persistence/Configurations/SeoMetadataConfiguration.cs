using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Central SEO table: one row per (EntityType, EntityId). No FK to the owner (polymorphic key).</summary>
public class SeoMetadataConfiguration : IEntityTypeConfiguration<SeoMetadata>
{
    public void Configure(EntityTypeBuilder<SeoMetadata> b)
    {
        b.ToTable("SeoMetadata");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityType).HasConversion<int>();
        b.Property(x => x.MetaTitle).HasMaxLength(300);
        b.Property(x => x.MetaDescription).HasMaxLength(500);
        b.Property(x => x.MetaKeywords).HasMaxLength(500);
        b.Property(x => x.CanonicalUrl).HasMaxLength(500);
        b.Property(x => x.H1).HasMaxLength(300);

        b.HasOne(x => x.MetaImage)
            .WithMany()
            .HasForeignKey(x => x.MetaImageMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.EntityType, x.EntityId }).IsUnique();
    }
}
