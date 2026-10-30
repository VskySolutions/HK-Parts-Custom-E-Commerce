using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> b)
    {
        b.ToTable("TenantBrandings");
        b.HasKey(x => x.Id);
        b.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Domain).HasMaxLength(255);
        b.Property(x => x.LogoUrl).HasMaxLength(500);
        b.Property(x => x.FaviconUrl).HasMaxLength(500);
        b.Property(x => x.PrimaryColor).HasMaxLength(32);
        b.Property(x => x.SecondaryColor).HasMaxLength(32);
        b.Property(x => x.AccentColor).HasMaxLength(32);
        b.Property(x => x.FontFamily).HasMaxLength(200);
        b.Property(x => x.SupportEmail).HasMaxLength(255);
        b.Property(x => x.SupportPhone).HasMaxLength(50);
        b.Property(x => x.DefaultLanguage).HasMaxLength(10);

        b.HasOne(x => x.LogoMedia)
            .WithMany()
            .HasForeignKey(x => x.LogoMediaId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.FaviconMedia)
            .WithMany()
            .HasForeignKey(x => x.FaviconMediaId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class SupportedCurrencyConfiguration : IEntityTypeConfiguration<SupportedCurrency>
{
    public void Configure(EntityTypeBuilder<SupportedCurrency> b)
    {
        b.ToTable("SupportedCurrencies");
        b.HasKey(x => x.Id);
        b.Property(x => x.CurrencyCode).HasMaxLength(3).IsRequired();
        b.Property(x => x.Symbol).HasMaxLength(8).IsRequired();
        b.Property(x => x.ExchangeRate).HasPrecision(18, 6);

        b.HasIndex(x => x.CurrencyCode).IsUnique();
    }
}

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> b)
    {
        b.ToTable("Stores");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ContactEmail).HasMaxLength(255);
        b.Property(x => x.ContactPhone).HasMaxLength(50);
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(x => x.CurrencyDisplay).HasMaxLength(3);
        b.Property(x => x.GuestOrderingEnabled).HasDefaultValue(true);

        // Shared postal address (never cascade-deleted with the store).
        b.HasOne(x => x.Address)
            .WithMany()
            .HasForeignKey(x => x.AddressId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasMany(x => x.DeliveryZones)
            .WithOne(z => z.Store!)
            .HasForeignKey(z => z.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class DeliveryZoneConfiguration : IEntityTypeConfiguration<DeliveryZone>
{
    public void Configure(EntityTypeBuilder<DeliveryZone> b)
    {
        b.ToTable("DeliveryZones");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.Region).HasMaxLength(120);
        b.Property(x => x.PostalCodeStart).HasMaxLength(20);
        b.Property(x => x.PostalCodeEnd).HasMaxLength(20);

        b.HasIndex(x => x.StoreId);
    }
}
