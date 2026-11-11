using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class OrderShippingConfiguration : IEntityTypeConfiguration<OrderShipping>
{
    public void Configure(EntityTypeBuilder<OrderShipping> b)
    {
        b.ToTable("OrderShippings");
        b.HasKey(x => x.Id);

        b.Property(x => x.MethodId).HasMaxLength(200);
        b.Property(x => x.MethodName).HasMaxLength(200);
        b.Property(x => x.DisplayName).HasMaxLength(300);
        b.Property(x => x.Carrier).HasMaxLength(100);
        b.Property(x => x.FflDealerName).HasMaxLength(300);
        b.Property(x => x.FflNumber).HasMaxLength(50);

        b.Property(x => x.MethodPrice).HasPrecision(18, 2);
        b.Property(x => x.HandlingFee).HasPrecision(18, 2);
        b.Property(x => x.SignatureFee).HasPrecision(18, 2);
        b.Property(x => x.InsuranceFee).HasPrecision(18, 2);
        b.Property(x => x.DeclaredValue).HasPrecision(18, 2);
        b.Property(x => x.ShippingTotal).HasPrecision(18, 2);
        b.Property(x => x.WeightLb).HasPrecision(18, 3);

        // Owned by its order: one row per shipment, removed with the order.
        b.HasOne(x => x.Order)
            .WithMany(o => o.Shippings)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.OrderId, x.ShipmentType });
    }
}

public class ShippingRuleSettingsConfiguration : IEntityTypeConfiguration<ShippingRuleSettings>
{
    public void Configure(EntityTypeBuilder<ShippingRuleSettings> b)
    {
        b.ToTable("ShippingRuleSettings");
        b.HasKey(x => x.Id);

        b.Property(x => x.SignatureFee).HasPrecision(18, 2);
        b.Property(x => x.InsuranceExcessFeePer100).HasPrecision(18, 2);
        b.Property(x => x.InsuranceBandsJson).HasMaxLength(4000);
        b.Property(x => x.RestrictedStates).HasMaxLength(1000).IsRequired();
        b.Property(x => x.AllowedCountryCodes).HasMaxLength(2000);
        b.Property(x => x.PoBoxMessage).HasMaxLength(500);
        b.Property(x => x.InvalidDestinationMessage).HasMaxLength(500);
    }
}
