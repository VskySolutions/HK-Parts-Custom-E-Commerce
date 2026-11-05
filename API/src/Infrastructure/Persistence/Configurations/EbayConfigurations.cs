using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class EbayAccountConfiguration : IEntityTypeConfiguration<EbayAccount>
{
    public void Configure(EntityTypeBuilder<EbayAccount> b)
    {
        b.ToTable("EbayAccounts");
        b.HasKey(x => x.StoreId);
        b.Property(x => x.StoreId).ValueGeneratedNever();
        b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        b.Property(x => x.Environment).HasConversion<byte>();
        b.Property(x => x.MarketplaceId).HasMaxLength(32).IsRequired().HasDefaultValue("EBAY_US");
        b.Property(x => x.CategoryTreeId).HasMaxLength(16);
        b.Property(x => x.ConnectedEbayUser).HasMaxLength(128);
        b.Property(x => x.DefaultCategoryId).HasMaxLength(20);
        b.Property(x => x.DefaultPaymentPolicyId).HasMaxLength(40);
        b.Property(x => x.DefaultFulfillmentPolicyId).HasMaxLength(40);
        b.Property(x => x.DefaultReturnPolicyId).HasMaxLength(40);
        b.Property(x => x.MerchantLocationKey).HasMaxLength(36);
    }
}

public class EbayListingConfiguration : IEntityTypeConfiguration<EbayListing>
{
    public void Configure(EntityTypeBuilder<EbayListing> b)
    {
        b.ToTable("EbayListings");
        b.HasKey(x => x.Id);
        b.Property(x => x.Sku).HasMaxLength(50).IsRequired();
        b.Property(x => x.CategoryId).HasMaxLength(20).IsRequired();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Title).HasMaxLength(80).IsRequired();
        b.Property(x => x.Subtitle).HasMaxLength(55);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.QuantitySold).HasDefaultValue(0);
        b.Property(x => x.ConditionId).HasMaxLength(20);
        b.Property(x => x.PaymentPolicyId).HasMaxLength(40);
        b.Property(x => x.FulfillmentPolicyId).HasMaxLength(40);
        b.Property(x => x.ReturnPolicyId).HasMaxLength(40);
        b.Property(x => x.OfferId).HasMaxLength(40);
        b.Property(x => x.EbayListingIdExternal).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);

        // A product can have only one non-ended listing.
        b.HasIndex(x => x.ProductId).IsUnique()
            .HasFilter($"[Status] <> {(byte)EbayListingStatus.Ended}")
            .HasDatabaseName("UX_EbayListings_Product_NotEnded");
        b.HasIndex(x => new { x.StoreId, x.Status });
        b.HasIndex(x => x.EbayListingIdExternal);
        b.HasIndex(x => x.Sku);
    }
}

public class EbayListingAspectConfiguration : IEntityTypeConfiguration<EbayListingAspect>
{
    public void Configure(EntityTypeBuilder<EbayListingAspect> b)
    {
        b.ToTable("EbayListingAspects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.HasOne(x => x.EbayListing).WithMany(l => l.Aspects)
            .HasForeignKey(x => x.EbayListingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EbayCategoryConfiguration : IEntityTypeConfiguration<EbayCategory>
{
    public void Configure(EntityTypeBuilder<EbayCategory> b)
    {
        b.ToTable("EbayCategories");
        b.HasKey(x => x.CategoryId);
        b.Property(x => x.CategoryId).HasMaxLength(20).ValueGeneratedNever();
        b.Property(x => x.ParentCategoryId).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TreeVersion).HasMaxLength(20);
        b.HasIndex(x => x.ParentCategoryId);
        b.HasIndex(x => x.Name);
    }
}

public class EbayApiLogConfiguration : IEntityTypeConfiguration<EbayApiLog>
{
    public void Configure(EntityTypeBuilder<EbayApiLog> b)
    {
        b.ToTable("EbayApiLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Operation).HasMaxLength(100).IsRequired();
        b.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
        b.Property(x => x.Url).HasMaxLength(2000).IsRequired();
        b.HasIndex(x => new { x.StoreId, x.CreatedOnUtc });
    }
}

public class VehicleFitmentConfiguration : IEntityTypeConfiguration<VehicleFitment>
{
    public void Configure(EntityTypeBuilder<VehicleFitment> b)
    {
        b.ToTable("VehicleFitments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Make).HasMaxLength(60).IsRequired();
        b.Property(x => x.Model).HasMaxLength(60).IsRequired();
        b.Property(x => x.Trim).HasMaxLength(120);
        b.Property(x => x.Engine).HasMaxLength(120);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ProductId, x.Year });
    }
}
