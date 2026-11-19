using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class ShipStationSettingsConfiguration : IEntityTypeConfiguration<ShipStationSettings>
{
    public void Configure(EntityTypeBuilder<ShipStationSettings> b)
    {
        b.ToTable("ShipStationSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(200);
        // Password is [Encrypted]: left unbounded (nvarchar(max)) like the credential tables, to fit cipher text.
        b.Property(x => x.ExportStatuses).HasMaxLength(200).IsRequired();
        b.Property(x => x.TimeZone).HasMaxLength(100).IsRequired();
    }
}

public class ShipStationLogConfiguration : IEntityTypeConfiguration<ShipStationLog>
{
    public void Configure(EntityTypeBuilder<ShipStationLog> b)
    {
        b.ToTable("ShipStationLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(20).IsRequired();
        b.Property(x => x.OrderNumber).HasMaxLength(64);
        b.Property(x => x.Error).HasMaxLength(1000);
        b.Property(x => x.ClientIp).HasMaxLength(64);
        b.HasIndex(x => x.CreatedOnUtc);
        b.HasIndex(x => new { x.Action, x.CreatedOnUtc });
    }
}

public class AddressVerificationSettingsConfiguration : IEntityTypeConfiguration<AddressVerificationSettings>
{
    public void Configure(EntityTypeBuilder<AddressVerificationSettings> b)
    {
        b.ToTable("AddressVerificationSettings");
        b.HasKey(x => x.Id);
        b.Property(x => x.FailedMessage).HasMaxLength(255);
        b.Property(x => x.Countries).HasMaxLength(1000);
    }
}

public class AddressVerificationResultConfiguration : IEntityTypeConfiguration<AddressVerificationResult>
{
    public void Configure(EntityTypeBuilder<AddressVerificationResult> b)
    {
        b.ToTable("AddressVerificationResults");
        b.HasKey(x => x.Id);
        b.Property(x => x.AddressHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.SuggestedJson).HasMaxLength(2000);
        b.Property(x => x.Messages).HasMaxLength(2000);
        b.HasIndex(x => new { x.AddressHash, x.Provider, x.CreatedOnUtc });
        b.HasIndex(x => x.CreatedOnUtc);
    }
}
