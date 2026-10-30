using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ShippingMethodConfiguration : IEntityTypeConfiguration<ShippingMethod>
{
    public void Configure(EntityTypeBuilder<ShippingMethod> b)
    {
        b.ToTable("ShippingMethods");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.MethodType).HasConversion<int>();
        b.Property(x => x.FlatRate).HasPrecision(18, 2);
        b.Property(x => x.FreeShippingThreshold).HasPrecision(18, 2);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ShippingZoneConfiguration : IEntityTypeConfiguration<ShippingZone>
{
    public void Configure(EntityTypeBuilder<ShippingZone> b)
    {
        b.ToTable("ShippingZones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.Region).HasMaxLength(120);
        b.Property(x => x.PostalCodeStart).HasMaxLength(20);
        b.Property(x => x.PostalCodeEnd).HasMaxLength(20);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class ShippingMethodZoneRateConfiguration : IEntityTypeConfiguration<ShippingMethodZoneRate>
{
    public void Configure(EntityTypeBuilder<ShippingMethodZoneRate> b)
    {
        b.ToTable("ShippingMethodZoneRates");
        b.HasKey(x => x.Id);
        b.Property(x => x.Rate).HasPrecision(18, 2);

        b.HasOne(x => x.ShippingMethod)
            .WithMany(m => m.ZoneRates)
            .HasForeignKey(x => x.ShippingMethodId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.ShippingZone)
            .WithMany(z => z.MethodRates)
            .HasForeignKey(x => x.ShippingZoneId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => new { x.ShippingMethodId, x.ShippingZoneId }).IsUnique();
    }
}

public class ShippingProviderConfigurationConfiguration : IEntityTypeConfiguration<ShippingProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<ShippingProviderConfiguration> b)
    {
        b.ToTable("ShippingProviderConfigurations");
        b.HasKey(x => x.Id);
        b.Property(x => x.SelectionMode).HasConversion<int>();
    }
}

public class ShippingCarrierSettingConfiguration : IEntityTypeConfiguration<ShippingCarrierSetting>
{
    public void Configure(EntityTypeBuilder<ShippingCarrierSetting> b)
    {
        b.ToTable("ShippingCarrierSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Carrier).HasConversion<int>();

        b.HasOne(x => x.Configuration)
            .WithMany(c => c.Carriers)
            .HasForeignKey(x => x.ShippingProviderConfigurationId)
            .OnDelete(DeleteBehavior.Cascade);

        // One row per carrier per configuration — the upsert relies on this.
        b.HasIndex(x => new { x.ShippingProviderConfigurationId, x.Carrier }).IsUnique();
    }
}
