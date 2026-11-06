using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.ToTable("Carts");
        b.HasKey(x => x.Id);
        b.Property(x => x.SessionId).HasMaxLength(128);
        b.Property(x => x.AppliedCouponCode).HasMaxLength(64);
        b.Property(x => x.CurrencyCode).HasMaxLength(3);
        b.Property(x => x.PointsDiscountAmount).HasPrecision(18, 2);

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.CustomerId);
        b.HasIndex(x => x.SessionId);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.ToTable("CartItems");
        b.Property(x => x.GcSenderName).HasMaxLength(75);
        b.Property(x => x.GcSenderEmail).HasMaxLength(75);
        b.Property(x => x.GcRecipientName).HasMaxLength(75);
        b.Property(x => x.GcRecipientEmail).HasMaxLength(75);
        b.Property(x => x.GcMessage).HasMaxLength(1000);
        b.HasKey(x => x.Id);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);

        b.HasOne(x => x.Cart)
            .WithMany(c => c.Items)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.CartId);
    }
}
