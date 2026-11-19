using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class FflDealerConfiguration : IEntityTypeConfiguration<FflDealer>
{
    public void Configure(EntityTypeBuilder<FflDealer> b)
    {
        b.ToTable("FflDealers");
        b.HasKey(x => x.Id);

        b.Property(x => x.Key).HasMaxLength(20).IsRequired();
        b.Property(x => x.LicRegn).HasMaxLength(2).IsRequired();
        b.Property(x => x.LicDist).HasMaxLength(3).IsRequired();
        b.Property(x => x.LicCnty).HasMaxLength(5).IsRequired();
        b.Property(x => x.LicType).HasMaxLength(3).IsRequired();
        b.Property(x => x.LicXprdte).HasMaxLength(4).IsRequired();
        b.Property(x => x.LicSeqn).HasMaxLength(10).IsRequired();
        b.Property(x => x.LicenseName).HasMaxLength(255);
        b.Property(x => x.BusinessName).HasMaxLength(255);
        b.Property(x => x.PremiseStreet).HasMaxLength(200);
        b.Property(x => x.PremiseCity).HasMaxLength(100);
        b.Property(x => x.PremiseState).HasMaxLength(50);
        b.Property(x => x.PremiseZipCode).HasMaxLength(20);
        b.Property(x => x.MailStreet).HasMaxLength(200);
        b.Property(x => x.MailCity).HasMaxLength(100);
        b.Property(x => x.MailState).HasMaxLength(50);
        b.Property(x => x.MailZipCode).HasMaxLength(20);
        b.Property(x => x.VoicePhone).HasMaxLength(30);
        b.Property(x => x.LastTransferFee).HasPrecision(18, 2);
        b.Property(x => x.AverageReviewRating).HasPrecision(5, 2);

        // FFL-122: unique key; the radius search pre-filters on type + bounding box.
        b.HasIndex(x => x.Key).IsUnique();
        b.HasIndex(x => new { x.LicType, x.Latitude, x.Longitude })
            .IncludeProperties(x => new { x.IsActive, x.AllowsTransfers });
        b.HasIndex(x => x.PremiseZipCode);
    }
}

public class ZipGeocodeConfiguration : IEntityTypeConfiguration<ZipGeocode>
{
    public void Configure(EntityTypeBuilder<ZipGeocode> b)
    {
        b.ToTable("ZipGeocodes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Zip).HasMaxLength(10).IsRequired();
        b.Property(x => x.Source).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Zip).IsUnique();
    }
}
