using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> b)
    {
        b.ToTable("Addresses");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).HasMaxLength(200);
        b.Property(x => x.LastName).HasMaxLength(200);
        b.Property(x => x.Company).HasMaxLength(200);
        b.Property(x => x.AddressLine1).HasMaxLength(255).IsRequired();
        b.Property(x => x.AddressLine2).HasMaxLength(255);
        b.Property(x => x.Landmark).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(120).IsRequired();
        b.Property(x => x.StateProvince).HasMaxLength(120);
        b.Property(x => x.PostalCode).HasMaxLength(20).IsRequired();
        b.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        b.Property(x => x.PhoneNumber).HasMaxLength(50);
        b.Property(x => x.Email).HasMaxLength(256);

        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> b)
    {
        b.ToTable("CustomerAddresses");
        b.HasKey(x => x.Id);
        b.Property(x => x.AddressType).HasConversion<int>();

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        // The shared address row outlives the mapping (never cascade-deleted with the book entry).
        b.HasOne(x => x.Address)
            .WithMany()
            .HasForeignKey(x => x.AddressId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.CustomerId);
        // At most one default per (customer, type) — enforced in the DB (AC-CUS-002.3).
        b.HasIndex(x => new { x.CustomerId, x.AddressType })
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [Deleted] = 0");

        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class UserTokenConfiguration : IEntityTypeConfiguration<UserToken>
{
    public void Configure(EntityTypeBuilder<UserToken> b)
    {
        b.ToTable("UserTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Purpose).HasConversion<int>();
        b.Property(x => x.TokenHash).HasMaxLength(512).IsRequired();

        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => x.TokenHash);
        b.HasIndex(x => new { x.UserId, x.Purpose });
    }
}
