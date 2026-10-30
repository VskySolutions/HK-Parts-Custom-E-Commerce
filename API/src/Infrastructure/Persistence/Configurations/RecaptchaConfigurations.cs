using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class RecaptchaConfigConfiguration : IEntityTypeConfiguration<RecaptchaConfig>
{
    public void Configure(EntityTypeBuilder<RecaptchaConfig> b)
    {
        b.ToTable("RecaptchaConfigs");
        b.HasKey(x => x.Id);
        b.Property(x => x.SiteKey).HasMaxLength(200);
        b.Property(x => x.SecretKeyLast4).HasMaxLength(8);
        b.Property(x => x.Version).HasConversion<int>();
        b.Property(x => x.FailBehaviour).HasConversion<int>();
        b.Property(x => x.ScoreThreshold).HasPrecision(3, 2);
        b.Ignore(x => x.IsConfigured);
    }
}
