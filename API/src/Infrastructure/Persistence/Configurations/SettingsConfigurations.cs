using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class PlatformSettingConfiguration : IEntityTypeConfiguration<PlatformSetting>
{
    public void Configure(EntityTypeBuilder<PlatformSetting> b)
    {
        b.ToTable("PlatformSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Key).HasMaxLength(200).IsRequired();
        b.Property(x => x.ValueType).HasMaxLength(50).IsRequired();
        b.Property(x => x.Category).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(500);

        b.HasIndex(x => x.Key).IsUnique();
    }
}

public class SettingsChangeHistoryConfiguration : IEntityTypeConfiguration<SettingsChangeHistory>
{
    public void Configure(EntityTypeBuilder<SettingsChangeHistory> b)
    {
        b.ToTable("SettingsChangeHistory");
        b.HasKey(x => x.Id);
        b.Property(x => x.SettingKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.ActorName).HasMaxLength(200);

        b.HasIndex(x => new { x.SettingKey, x.ChangedOnUtc });
    }
}
