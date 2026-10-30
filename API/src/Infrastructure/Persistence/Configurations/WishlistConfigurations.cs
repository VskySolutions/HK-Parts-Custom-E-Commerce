using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> b)
    {
        b.ToTable("Wishlists");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        b.HasIndex(x => x.CustomerId);
        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> b)
    {
        b.ToTable("WishlistItems");
        b.HasKey(x => x.Id);

        b.HasOne(x => x.Wishlist)
            .WithMany(w => w.Items)
            .HasForeignKey(x => x.WishlistId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.WishlistId, x.ProductId });
    }
}
