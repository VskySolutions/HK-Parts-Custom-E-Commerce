using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Username).HasMaxLength(256).IsRequired();
        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();

        // Unique username + email among non-deleted rows.
        b.HasIndex(x => x.Username).IsUnique().HasFilter("[Deleted] = 0");
        b.HasIndex(x => x.Email).IsUnique().HasFilter("[Deleted] = 0");
        b.HasQueryFilter(x => !x.Deleted);

        // 1:1 with Customer.
        b.HasOne(x => x.Customer)
            .WithOne(c => c.User!)
            .HasForeignKey<Customer>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.RefreshTokens)
            .WithOne(x => x.User!)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasMany(x => x.UserRoles)
            .WithOne(ur => ur.User!)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("Roles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasIndex(x => x.NormalizedName).IsUnique();

        b.HasMany(x => x.UserRoles)
            .WithOne(ur => ur.Role!)
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("UserRoles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasIndex(x => x.RoleId);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).HasMaxLength(200).IsRequired();
        b.Property(x => x.LastName).HasMaxLength(200);
        b.Property(x => x.PhoneNumber).HasMaxLength(50);
        b.Property(x => x.TaxExemptionCertificate).HasMaxLength(200);
        b.Property(x => x.VatId).HasMaxLength(64);
        b.Property(x => x.WhatsAppPhoneNumber).HasMaxLength(50);

        // Pricing group (AC-CUS-003.2): optional, at most one. Restrict — a lookup FK must never
        // cascade a group delete into customer rows.
        b.HasOne(x => x.CustomerGroup)
            .WithMany()
            .HasForeignKey(x => x.CustomerGroupId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CustomerGroupId);

        b.HasQueryFilter(x => !x.Deleted);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Ignore(x => x.IsActive);

        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.RevokedOnUtc });
    }
}
