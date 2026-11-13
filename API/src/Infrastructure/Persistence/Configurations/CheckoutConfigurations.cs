using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class CheckoutLockConfiguration : IEntityTypeConfiguration<CheckoutLock>
{
    public void Configure(EntityTypeBuilder<CheckoutLock> b)
    {
        // The table name is used by CheckoutLockService's MERGE; keep them in step.
        b.ToTable("CheckoutLocks");
        b.HasKey(x => x.Id);
        // One lock per product (CK-20).
        b.HasIndex(x => x.ProductId).IsUnique();
        b.HasIndex(x => x.OwnerCartId);
    }
}

public class CustomerBlacklistEntryConfiguration : IEntityTypeConfiguration<CustomerBlacklistEntry>
{
    public void Configure(EntityTypeBuilder<CustomerBlacklistEntry> b)
    {
        b.ToTable("CustomerBlacklist");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.Reason).HasMaxLength(1000);
        b.Property(x => x.Status).HasConversion<int>();
        b.HasIndex(x => x.Email);
        b.HasIndex(x => new { x.LastName, x.FirstName });
    }
}

public class CustomerBlacklistHitConfiguration : IEntityTypeConfiguration<CustomerBlacklistHit>
{
    public void Configure(EntityTypeBuilder<CustomerBlacklistHit> b)
    {
        b.ToTable("CustomerBlacklistHits");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Email).HasMaxLength(256);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.MatchedOn).HasMaxLength(20);
        // Hits are owned by their entry: deleting the entry removes its log.
        b.HasOne(x => x.BlacklistEntry).WithMany(x => x.Hits)
            .HasForeignKey(x => x.BlacklistEntryId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.AtUtc);
    }
}
