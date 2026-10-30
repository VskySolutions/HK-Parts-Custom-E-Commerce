using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> b)
    {
        b.ToTable("Subscriptions");
        b.HasKey(x => x.Id);

        b.Property(x => x.Interval).HasConversion<int>();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.PaymentMethodRef).HasMaxLength(200);

        // Lookup FKs — never cascade-delete a subscription with its customer/product/address, and keep a
        // single cascade path to any principal (SQL Server multiple-cascade-path guard).
        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // Shared saved shipping address (no navigation; snapshotted onto generated orders).
        b.HasOne<Address>()
            .WithMany()
            .HasForeignKey(x => x.ShippingAddressId)
            .OnDelete(DeleteBehavior.NoAction);

        // The due-order scan filters Active subs by NextOrderOnUtc; this index serves that hot path.
        b.HasIndex(x => new { x.Status, x.NextOrderOnUtc });
        b.HasIndex(x => x.CustomerId);

        b.HasQueryFilter(x => !x.Deleted);
    }
}
