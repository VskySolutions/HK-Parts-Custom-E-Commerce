using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HKPartsEcommerce.Domain.Entities;

namespace HKPartsEcommerce.Infrastructure.Persistence.Configurations;

/// <summary>
/// Blacklisted customers. Customer is a NoAction FK (customers are soft-deleted, never removed). A filtered unique
/// index allows only one active (Deleted = 0) record per customer; soft-deleted rows are kept as history.
/// </summary>
public class BlacklistedCustomerConfiguration : IEntityTypeConfiguration<BlacklistedCustomer>
{
    public void Configure(EntityTypeBuilder<BlacklistedCustomer> b)
    {
        b.ToTable("BlacklistedCustomers");
        b.HasKey(x => x.Id);
        b.Property(x => x.Reason).HasMaxLength(BlacklistedCustomer.ReasonMaxLength).IsRequired();
        b.Property(x => x.InternalNotes).HasMaxLength(BlacklistedCustomer.InternalNotesMaxLength);
        // Column default 0; EF always sends the value itself (no store-generated sentinel).
        b.Property(x => x.Deleted).HasDefaultValue(false).ValueGeneratedNever();

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        // Lookup / NOT EXISTS on CustomerId (all rows, incl. history).
        b.HasIndex(x => x.CustomerId, "IX_BlacklistedCustomers_CustomerId");
        // One active blacklist record per customer.
        b.HasIndex(x => x.CustomerId, "UX_BlacklistedCustomers_CustomerId_Active")
            .IsUnique()
            .HasFilter("[Deleted] = 0");

        b.HasQueryFilter(x => !x.Deleted);
    }
}
