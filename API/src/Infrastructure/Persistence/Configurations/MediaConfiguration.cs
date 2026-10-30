using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>EF configuration for the central Media library (WO-122).</summary>
public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> b)
    {
        b.ToTable("Media");
        b.HasKey(x => x.Id);

        b.Property(x => x.OriginalFileName).IsRequired().HasMaxLength(400);
        b.Property(x => x.SeoFileName).IsRequired().HasMaxLength(400);
        b.Property(x => x.AssetKey).IsRequired().HasMaxLength(1024);
        b.Property(x => x.Url).HasMaxLength(2048);
        b.Property(x => x.MediaType).HasConversion<int>();
        b.Property(x => x.MimeType).IsRequired().HasMaxLength(200);
        b.Property(x => x.AltText).HasMaxLength(500);
        b.Property(x => x.Title).HasMaxLength(300);
        b.Property(x => x.Caption).HasMaxLength(500);

        // SEO file name is the path component + a public identifier — unique among live rows (reusable after delete).
        b.HasIndex(x => x.SeoFileName).IsUnique().HasFilter("[Deleted] = 0");
        b.HasIndex(x => x.MediaType);

        b.HasQueryFilter(x => !x.Deleted);
    }
}
