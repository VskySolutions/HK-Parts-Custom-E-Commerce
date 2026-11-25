using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class TaxProviderConfigurationConfiguration : IEntityTypeConfiguration<TaxProviderConfiguration>
{
    public void Configure(EntityTypeBuilder<TaxProviderConfiguration> b)
    {
        b.ToTable("TaxProviderConfigurations");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActiveProvider).HasConversion<int>();
        b.Property(x => x.FlatRatePercent).HasPrecision(9, 4);
        b.Property(x => x.TaxShipping).HasDefaultValue(true);
        b.Property(x => x.AvalaraCommitDocuments).HasDefaultValue(true);
        b.Property(x => x.AvalaraLoggingEnabled).HasDefaultValue(true);
        b.Property(x => x.AvalaraTimeoutSeconds).HasDefaultValue(10);
        b.Property(x => x.AvalaraShippingTaxCode).HasMaxLength(50);
    }
}

public class TaxStateRateConfiguration : IEntityTypeConfiguration<TaxStateRate>
{
    public void Configure(EntityTypeBuilder<TaxStateRate> b)
    {
        b.ToTable("TaxStateRates");
        b.HasKey(x => x.Id);
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.StateCode).HasMaxLength(3).IsRequired();
        b.Property(x => x.StateName).HasMaxLength(100).IsRequired();
        b.Property(x => x.RatePercent).HasPrecision(9, 4);
        b.HasIndex(x => new { x.CountryCode, x.StateCode }).IsUnique();
    }
}

public class TaxZipRateConfiguration : IEntityTypeConfiguration<TaxZipRate>
{
    public void Configure(EntityTypeBuilder<TaxZipRate> b)
    {
        b.ToTable("TaxZipRates");
        b.HasKey(x => x.Id);
        b.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.RatePercent).HasPrecision(9, 4);
        b.HasIndex(x => x.PostalCode).IsUnique();
    }
}

public class TaxLogConfiguration : IEntityTypeConfiguration<TaxLog>
{
    public void Configure(EntityTypeBuilder<TaxLog> b)
    {
        b.ToTable("TaxLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasMaxLength(30).IsRequired();
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.OrderNumber).HasMaxLength(50);
        b.Property(x => x.Url).HasMaxLength(500);
        b.Property(x => x.ErrorMessage).HasMaxLength(1000);
        b.HasIndex(x => x.CreatedOnUtc);
    }
}
