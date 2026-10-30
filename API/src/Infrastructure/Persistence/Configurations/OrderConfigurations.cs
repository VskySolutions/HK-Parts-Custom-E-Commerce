using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("Orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.Subtotal).HasPrecision(18, 2);
        b.Property(x => x.DiscountTotal).HasPrecision(18, 2);
        b.Property(x => x.ShippingTotal).HasPrecision(18, 2);
        b.Property(x => x.TaxTotal).HasPrecision(18, 2);
        b.Property(x => x.PaymentFeePercent).HasPrecision(5, 2);
        b.Property(x => x.PaymentFeeTotal).HasPrecision(18, 2);
        b.Property(x => x.PointsDiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.AppliedCouponCode).HasMaxLength(64);
        b.Property(x => x.ShippingMethodId).HasMaxLength(200);
        b.Property(x => x.ShippingMethodName).HasMaxLength(200);
        b.Property(x => x.ShippingCarrier).HasMaxLength(100);
        b.Property(x => x.TrackingNumber).HasMaxLength(100);
        b.Property(x => x.PaymentStatus).HasConversion<int>();
        b.Property(x => x.InquiryStatus).HasConversion<int>();
        b.Property(x => x.PreferredContact).HasConversion<int>();
        b.Property(x => x.CustomerNote).HasMaxLength(4000);
        b.Property(x => x.InternalNotes).HasMaxLength(4000);
        b.Property(x => x.CompanyName).HasMaxLength(200);

        // Inquiries are listed on their own admin page and excluded from every order/revenue query;
        // both sides of the flag are queried, so index it.
        b.HasIndex(x => x.IsInquiry);

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.AssignedStore)
            .WithMany()
            .HasForeignKey(x => x.AssignedStoreId)
            .OnDelete(DeleteBehavior.NoAction);

        // Shared delivery address (never cascade-deleted with the order).
        b.HasOne(x => x.ShippingAddress)
            .WithMany()
            .HasForeignKey(x => x.ShippingAddressId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.OrderNumber).IsUnique();
        b.HasIndex(x => new { x.AssignedStoreId, x.Status });
        b.HasIndex(x => x.Status);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
{
    public void Configure(EntityTypeBuilder<OrderLineItem> b)
    {
        b.ToTable("OrderLineItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.ProductName).HasMaxLength(400).IsRequired();
        b.Property(x => x.Sku).HasMaxLength(100);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.OriginalUnitPrice).HasPrecision(18, 2);
        b.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);

        b.HasOne(x => x.Order)
            .WithMany(o => o.Lines)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.OrderId);
        b.HasIndex(x => x.ProductId);
    }
}

public class StoreManagerAssignmentConfiguration : IEntityTypeConfiguration<StoreManagerAssignment>
{
    public void Configure(EntityTypeBuilder<StoreManagerAssignment> b)
    {
        b.ToTable("StoreManagerAssignments");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => x.StoreId)
            .OnDelete(DeleteBehavior.NoAction);

        // One store per manager.
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => x.StoreId);
    }
}
