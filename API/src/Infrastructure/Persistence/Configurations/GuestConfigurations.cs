using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>Guest checkout §5.2: one row per normalized guest email.</summary>
public class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> b)
    {
        b.ToTable("Guests");
        b.HasKey(x => x.Id);
        b.Property(x => x.EmailNormalized).HasMaxLength(256).IsRequired();
        b.HasIndex(x => x.EmailNormalized).IsUnique();
        b.Property(x => x.FirstName).HasMaxLength(200);
        b.Property(x => x.LastName).HasMaxLength(200);
        b.Property(x => x.PhoneNumber).HasMaxLength(50);
        b.HasIndex(x => x.LastOrderOnUtc);
        // Customers are soft-deleted, never removed: lookup FK, no cascade.
        b.HasOne(x => x.LinkedCustomer).WithMany().HasForeignKey(x => x.LinkedCustomerId).OnDelete(DeleteBehavior.NoAction);
        b.HasIndex(x => x.LinkedCustomerId);
    }
}

/// <summary>Guest order lookup attempts (U-62 rate limit + audit). No order FK: the log outlives anything.</summary>
public class GuestOrderAccessLogConfiguration : IEntityTypeConfiguration<GuestOrderAccessLog>
{
    public void Configure(EntityTypeBuilder<GuestOrderAccessLog> b)
    {
        b.ToTable("GuestOrderAccessLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Method).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.IpAddress, x.AtUtc });
        b.HasIndex(x => x.OrderId);
    }
}
