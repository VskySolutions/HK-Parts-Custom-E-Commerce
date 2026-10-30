using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class StateNexusAccumulatorConfiguration : IEntityTypeConfiguration<StateNexusAccumulator>
{
    public void Configure(EntityTypeBuilder<StateNexusAccumulator> b)
    {
        b.ToTable("StateNexusAccumulators");
        b.HasKey(x => x.Id);
        b.Property(x => x.StateCode).HasMaxLength(8).IsRequired();
        b.Property(x => x.GrossSales).HasPrecision(18, 2);
        b.Property(x => x.ThresholdAmount).HasPrecision(18, 2);
        b.Property(x => x.WarningPercent).HasPrecision(5, 4);

        b.HasIndex(x => new { x.StateCode, x.PeriodStartUtc }).IsUnique();
    }
}
