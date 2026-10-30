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
    }
}
